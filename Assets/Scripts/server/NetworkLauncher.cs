using FishNet;
using FishNet.Broadcast;
using FishNet.Managing.Scened;
using FishNet.Transporting;
using FishNet.Transporting.Bayou;
using System;
using System.Text;
using System.Net.Sockets;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum RoomAccessRequestType : byte
{
    Host = 1,
    Join = 2
}

public struct RoomAccessRequestBroadcast : IBroadcast
{
    public byte RequestType;
    public string RoomCode;
    public string Username;
    public string Password;
}

public struct RoomAccessResponseBroadcast : IBroadcast
{
    public bool Accepted;
    public string RoomCode;
    public string Username;
    public string Message;
}

public class NetworkLauncher : MonoBehaviour
{
    public static string LastTargetAddress { get; private set; } = "not selected";
    public static ushort LastTargetPort { get; private set; }
    public static int ConnectionAttemptId { get; private set; }

    [SerializeField] private string serverIP = "127.0.0.1";
    [SerializeField] private ushort port = 10000;
    [SerializeField] private TMP_InputField ipInput;
    [SerializeField] private TMP_InputField usernameInput;
    [SerializeField] private TMP_InputField accountPasswordInput;
    [SerializeField] private TMP_InputField passwordInput;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text roomCodeText;
    [SerializeField] private GameObject[] mainMenuObjects;
    [SerializeField] private GameObject[] multiplayerMenuObjects;
    [SerializeField] private bool preferLocalServerInEditor;
    [SerializeField] private string gameSceneName = "Game";

    private bool _subscribed;
    private bool _roomResponseSubscribed;
    private bool _isConnecting;
    private bool _startLocalHost;
    private bool _gameSceneLoadRequested;
    private RoomAccessRequestType? _pendingRoomRequest;
    private string _pendingRoomCode = string.Empty;
    private string _pendingUsername = string.Empty;
    private string _pendingPassword = string.Empty;

    private void Awake()
    {
        bool forceInputText = false;

        if (ipInput == null)
            ipInput = FindSceneComponentByName<TMP_InputField>("IP");

        if (passwordInput == null)
            passwordInput = FindSceneComponentByName<TMP_InputField>("PasswordInput");

        if (usernameInput == null)
            usernameInput = FindSceneComponentByName<TMP_InputField>("UsernameInput");

        if (accountPasswordInput == null)
            accountPasswordInput = FindSceneComponentByName<TMP_InputField>("AccountPasswordInput");

        if (statusText == null)
            statusText = FindSceneComponentByName<TMP_Text>("Status");

        if (roomCodeText == null)
            roomCodeText = FindSceneComponentByName<TMP_Text>("RoomCodeText");

#if UNITY_EDITOR
        if (preferLocalServerInEditor && !Application.isBatchMode && IsRenderAddress(serverIP))
        {
            serverIP = "127.0.0.1";
            port = 10000;
            forceInputText = true;
        }
#endif

        if (ipInput != null && (forceInputText || string.IsNullOrWhiteSpace(ipInput.text)))
            ipInput.text = serverIP;

        SetDiagnosticTarget(serverIP, port, incrementAttempt: false);
        EnsureRuntimeMultiplayerMenu();
        SetMultiplayerMenuVisible(false);
        SetStatus("Ready");
    }

    private void OnClientState(ClientConnectionStateArgs args)
    {
        Debug.Log("[ClientState] " + args.ConnectionState);

        if (args.ConnectionState == LocalConnectionState.Started)
        {
            _isConnecting = false;
            Debug.Log("[Client] CONNECT SUCCESS");

            if (_pendingRoomRequest.HasValue)
            {
                SendRoomAccessRequest();
                return;
            }

            SetStatus("Connected");
            LoadGameSceneAfterConnect();
        }
        else if (args.ConnectionState == LocalConnectionState.Starting)
        {
            _isConnecting = true;
            SetStatus("Connecting...");
        }
        else if (args.ConnectionState == LocalConnectionState.Stopping)
        {
            _isConnecting = false;
            SetStatus("Connection failed");
            Debug.LogWarning("[Client] CONNECT FAILED");
        }
        else if (args.ConnectionState == LocalConnectionState.Stopped)
        {
            _isConnecting = false;
            _pendingRoomRequest = null;
            SetStatus("Disconnected");
            Debug.Log("[Client] CONNECT STOPPED");
        }
    }

    public void OnJoinClick()
    {
        if (_isConnecting)
        {
            Debug.Log("[NetworkLauncher] Already connecting.");
            return;
        }

        string targetAddress = GetTargetAddress();
        ushort targetPort = GetTargetPort(targetAddress);
        SetDiagnosticTarget(targetAddress, targetPort, incrementAttempt: true);

        Debug.Log("[NetworkLauncher] OnJoinClick called");
        Debug.Log("[Client] Target IP: " + targetAddress + ", Port: " + targetPort);

        if (InstanceFinder.NetworkManager == null)
        {
            SetStatus("NetworkManager missing");
            Debug.LogError("NetworkManager is missing!");
            return;
        }

        if (InstanceFinder.ClientManager.Started)
        {
            SetStatus("Already connected");
            Debug.Log("[NetworkLauncher] Client is already connected.");
            return;
        }

        var bayou = InstanceFinder.NetworkManager.GetComponent<Bayou>();
        if (bayou == null)
        {
            SetStatus("Bayou transport missing");
            Debug.LogError("Bayou not found on NetworkManager!");
            return;
        }

        if (!_subscribed)
        {
            InstanceFinder.ClientManager.OnClientConnectionState += OnClientState;
            _subscribed = true;
        }

        RegisterRoomResponseHandler();
        serverIP = targetAddress;
        port = targetPort;
        bool useWss = IsRenderAddress(serverIP);
        bayou.SetClientAddress(serverIP);
        bayou.SetPort(port);
        bayou.SetUseWSS(useWss);

        if (_startLocalHost)
            StartLocalServerIfNeeded(bayou);

        SetStatus("Connecting...");
        _isConnecting = true;
        _gameSceneLoadRequested = false;
        InstanceFinder.ClientManager.StartConnection();
    }

    public void OnPlayLocalClick()
    {
        _startLocalHost = true;
        SetAddress("127.0.0.1", 10000);
        OnJoinClick();
    }

    public void OnMultiplayerRenderClick()
    {
        _pendingRoomRequest = null;
        _pendingRoomCode = string.Empty;
        _pendingUsername = string.Empty;
        _pendingPassword = string.Empty;
        SetAddress("survive-server-rl6m.onrender.com", 443);
        SetMultiplayerMenuVisible(true);
        SetRoomCode("Login, then host or join");
        SetStatus("Choose Host or Join");
    }

    public void OnBackFromMultiplayerClick()
    {
        _pendingRoomRequest = null;
        _pendingRoomCode = string.Empty;
        _pendingUsername = string.Empty;
        _pendingPassword = string.Empty;
        SetMultiplayerMenuVisible(false);
        SetRoomCode(string.Empty);
        SetStatus("Ready");
    }

    public void OnHostRenderClick()
    {
        if (!TryReadAccountInputs(out string username, out string password))
            return;

        _startLocalHost = false;
        _pendingRoomRequest = RoomAccessRequestType.Host;
        _pendingRoomCode = GenerateRoomCode();
        _pendingUsername = username;
        _pendingPassword = password;
        SetRoomCode($"Password: {_pendingRoomCode}");
        SetAddress("survive-server-rl6m.onrender.com", 443);
        SetStatus("Creating room...");
        OnJoinClick();
    }

    public void OnJoinRenderClick()
    {
        if (!TryReadAccountInputs(out string username, out string password))
            return;

        string code = passwordInput == null ? string.Empty : NormalizeRoomCode(passwordInput.text);
        if (string.IsNullOrWhiteSpace(code))
        {
            SetStatus("Enter room password");
            return;
        }

        _startLocalHost = false;
        _pendingRoomRequest = RoomAccessRequestType.Join;
        _pendingRoomCode = code;
        _pendingUsername = username;
        _pendingPassword = password;
        SetRoomCode($"Joining: {_pendingRoomCode}");
        SetAddress("survive-server-rl6m.onrender.com", 443);
        SetStatus("Checking password...");
        OnJoinClick();
    }

    public void UseLocalServer()
    {
        SetAddress("127.0.0.1", 10000);
    }

    public void UseRenderServer()
    {
        SetAddress("survive-server-rl6m.onrender.com", 443);
    }

    private void StartLocalServerIfNeeded(Bayou bayou)
    {
        if (InstanceFinder.ServerManager.Started)
            return;

        bayou.SetPort(port);
        bayou.SetServerBindAddress("127.0.0.1", FishNet.Transporting.IPAddressType.IPv4);

        try
        {
            bool started = InstanceFinder.ServerManager.StartConnection();
            Debug.Log("[NetworkLauncher] Local host server start returned: " + started);

            if (!started)
                SetStatus("Local server failed");
        }
        catch (SocketException exception)
        {
            Debug.LogWarning("[NetworkLauncher] Local port is already in use. Connecting to existing local server instead. " + exception.Message);
            SetStatus("Using existing local server");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            SetStatus("Local server failed");
        }
    }

    private void LoadGameSceneAfterConnect()
    {
        if (_gameSceneLoadRequested)
            return;

        if (string.IsNullOrWhiteSpace(gameSceneName))
            return;

        _gameSceneLoadRequested = true;

        if (_startLocalHost && InstanceFinder.ServerManager.Started && InstanceFinder.SceneManager != null)
        {
            SceneLoadData sceneLoadData = new SceneLoadData(gameSceneName)
            {
                ReplaceScenes = ReplaceOption.All
            };

            InstanceFinder.SceneManager.LoadGlobalScenes(sceneLoadData);
            Debug.Log("[NetworkLauncher] Loading network scene: " + gameSceneName);
            return;
        }

        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != gameSceneName)
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(gameSceneName);
            Debug.Log("[NetworkLauncher] Loading local scene: " + gameSceneName);
        }
    }

    private void RegisterRoomResponseHandler()
    {
        if (_roomResponseSubscribed || InstanceFinder.ClientManager == null)
            return;

        InstanceFinder.ClientManager.RegisterBroadcast<RoomAccessResponseBroadcast>(OnRoomAccessResponse);
        _roomResponseSubscribed = true;
    }

    private void SendRoomAccessRequest()
    {
        if (!_pendingRoomRequest.HasValue)
            return;

        RoomAccessRequestBroadcast request = new RoomAccessRequestBroadcast
        {
            RequestType = (byte)_pendingRoomRequest.Value,
            RoomCode = _pendingRoomCode,
            Username = _pendingUsername,
            Password = _pendingPassword
        };

        string action = _pendingRoomRequest.Value == RoomAccessRequestType.Host ? "Creating room..." : "Joining room...";
        SetStatus(action);
        InstanceFinder.ClientManager.Broadcast(request);
    }

    private void OnRoomAccessResponse(RoomAccessResponseBroadcast response, Channel channel)
    {
        if (!response.Accepted)
        {
            _pendingRoomRequest = null;
            SetStatus(string.IsNullOrWhiteSpace(response.Message) ? "Password rejected" : response.Message);

            if (InstanceFinder.ClientManager != null && InstanceFinder.ClientManager.Started)
                InstanceFinder.ClientManager.StopConnection();

            return;
        }

        _pendingRoomCode = NormalizeRoomCode(response.RoomCode);
        SetRoomCode($"Password: {_pendingRoomCode}");
        SetStatus(string.IsNullOrWhiteSpace(response.Message) ? "Connected" : response.Message);
        _pendingRoomRequest = null;
        _pendingPassword = string.Empty;
        LoadGameSceneAfterConnect();
    }

    private bool TryReadAccountInputs(out string username, out string password)
    {
        username = usernameInput == null ? string.Empty : usernameInput.text.Trim();
        password = accountPasswordInput == null ? string.Empty : accountPasswordInput.text;

        if (string.IsNullOrWhiteSpace(username))
        {
            SetStatus("Enter player name");
            return false;
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            SetStatus("Enter account password");
            return false;
        }

        if (username.Length < 3 || username.Length > 16)
        {
            SetStatus("Name must be 3-16 chars");
            return false;
        }

        if (password.Length < 4 || password.Length > 64)
        {
            SetStatus("Password must be 4-64 chars");
            return false;
        }

        return true;
    }

    private string GetTargetAddress()
    {
        if (ipInput != null && !string.IsNullOrWhiteSpace(ipInput.text))
            return ipInput.text.Trim();

        return string.IsNullOrWhiteSpace(serverIP) ? "127.0.0.1" : serverIP.Trim();
    }

    private ushort GetTargetPort(string targetAddress)
    {
        if (IsLocalAddress(targetAddress) && port == 443)
            return 10000;

        if (IsRenderAddress(targetAddress) && port == 10000)
            return 443;

        return port;
    }

    private static bool IsLocalAddress(string address)
    {
        return address == "127.0.0.1" ||
               address == "localhost" ||
               address == "::1";
    }

    private static bool IsRenderAddress(string address)
    {
        return address.EndsWith(".onrender.com");
    }

    private static string GenerateRoomCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        StringBuilder builder = new StringBuilder(8);

        for (int i = 0; i < 8; i++)
            builder.Append(chars[UnityEngine.Random.Range(0, chars.Length)]);

        return builder.ToString();
    }

    private static string NormalizeRoomCode(string code)
    {
        return string.IsNullOrWhiteSpace(code) ? string.Empty : code.Trim().ToUpperInvariant();
    }

    private void SetMultiplayerMenuVisible(bool visible)
    {
        EnsureRuntimeMultiplayerMenu();
        SetObjectsActive(mainMenuObjects, !visible);
        SetObjectsActive(multiplayerMenuObjects, visible);
    }

    private void EnsureRuntimeMultiplayerMenu()
    {
        if (HasObjects(mainMenuObjects) && HasObjects(multiplayerMenuObjects))
            return;

        GameObject menuButtons = FindSceneGameObjectByName("MenuButtons");
        if (!HasObjects(mainMenuObjects) && menuButtons != null)
            mainMenuObjects = new[] { menuButtons };

        if (HasObjects(multiplayerMenuObjects) || menuButtons == null)
            return;

        RectTransform source = menuButtons.GetComponent<RectTransform>();
        RectTransform group = new GameObject("MultiplayerButtons", typeof(RectTransform)).GetComponent<RectTransform>();
        group.SetParent(source.parent, false);
        CopyRect(source, group);

        TMP_Text sourceLabel = FindTextInChildren(FindSceneGameObjectByName("MultiplayerButton"));
        roomCodeText = CreateRuntimeText("RoomCodeText", group, "Login, then host or join", 23, new Color(1f, 0.76f, 0.22f, 1f), sourceLabel);
        SetRuntimeRect(roomCodeText.rectTransform, new Vector2(0, 258), new Vector2(540, 44));

        usernameInput = CreateRuntimeInput(group, sourceLabel, "UsernameInput", "PLAYER NAME", false, new Vector2(0, 202), 16);
        accountPasswordInput = CreateRuntimeInput(group, sourceLabel, "AccountPasswordInput", "ACCOUNT PASSWORD", true, new Vector2(0, 136), 64);
        passwordInput = CreateRuntimeInput(group, sourceLabel, "PasswordInput", "ROOM PASSWORD (JOIN)", false, new Vector2(0, 70), 12);

        Button host = CloneRuntimeButton("MultiplayerButton", group, "HostButton", "HOST", new Vector2(0, -14));
        host.onClick.AddListener(OnHostRenderClick);

        Button join = CloneRuntimeButton("SettingsButton", group, "JoinButton", "JOIN", new Vector2(0, -132));
        join.onClick.AddListener(OnJoinRenderClick);

        Button back = CloneRuntimeButton("ExitButton", group, "BackButton", "BACK", new Vector2(0, -250));
        back.onClick.AddListener(OnBackFromMultiplayerClick);

        group.gameObject.SetActive(false);
        multiplayerMenuObjects = new[] { group.gameObject };
    }

    private static bool HasObjects(GameObject[] objects)
    {
        return objects != null && objects.Length > 0 && objects[0] != null;
    }

    private Button CloneRuntimeButton(string sourceName, RectTransform parent, string newName, string label, Vector2 position)
    {
        GameObject source = FindSceneGameObjectByName(sourceName);
        GameObject clone;

        if (source != null)
        {
            clone = Instantiate(source, parent, false);
            clone.name = newName;
        }
        else
        {
            clone = new GameObject(newName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            clone.transform.SetParent(parent, false);
            Image image = clone.GetComponent<Image>();
            image.color = new Color(0.045f, 0.07f, 0.11f, 0.84f);
        }

        SetRuntimeRect(clone.GetComponent<RectTransform>(), position, new Vector2(540, 92));

        Button button = clone.GetComponent<Button>();
        button.onClick = new Button.ButtonClickedEvent();

        TMP_Text text = FindTextInChildren(clone);
        if (text != null)
            text.text = label;

        return button;
    }

    private TMP_InputField CreateRuntimeInput(RectTransform parent, TMP_Text sourceLabel, string name, string placeholderValue, bool password, Vector2 position, int characterLimit)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(TMP_InputField));
        go.transform.SetParent(parent, false);
        SetRuntimeRect(go.GetComponent<RectTransform>(), position, new Vector2(540, 58));

        Image image = go.GetComponent<Image>();
        image.color = new Color(0.025f, 0.04f, 0.065f, 0.90f);

        TMP_Text placeholder = CreateRuntimeText("Placeholder", go.GetComponent<RectTransform>(), placeholderValue, 20, new Color(0.48f, 0.56f, 0.70f, 0.82f), sourceLabel);
        StretchRuntimeRect(placeholder.rectTransform, new Vector2(24, 0), new Vector2(-24, 0));

        TMP_Text text = CreateRuntimeText("Text", go.GetComponent<RectTransform>(), string.Empty, 22, new Color(0.92f, 0.94f, 0.98f, 1f), sourceLabel);
        StretchRuntimeRect(text.rectTransform, new Vector2(24, 0), new Vector2(-24, 0));

        TMP_InputField input = go.GetComponent<TMP_InputField>();
        input.textComponent = text;
        input.placeholder = placeholder;
        input.characterLimit = characterLimit;
        input.contentType = password ? TMP_InputField.ContentType.Password : TMP_InputField.ContentType.Alphanumeric;
        return input;
    }

    private static TMP_Text CreateRuntimeText(string name, RectTransform parent, string value, int fontSize, Color color, TMP_Text sourceLabel)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);

        TMP_Text text = go.GetComponent<TMP_Text>();
        text.text = value;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.fontStyle = FontStyles.Bold;
        text.raycastTarget = false;

        if (sourceLabel != null)
            text.font = sourceLabel.font;

        return text;
    }

    private static TMP_Text FindTextInChildren(GameObject go)
    {
        return go == null ? null : go.GetComponentInChildren<TMP_Text>(true);
    }

    private static void CopyRect(RectTransform source, RectTransform target)
    {
        target.anchorMin = source.anchorMin;
        target.anchorMax = source.anchorMax;
        target.pivot = source.pivot;
        target.anchoredPosition = source.anchoredPosition;
        target.sizeDelta = source.sizeDelta;
    }

    private static void SetRuntimeRect(RectTransform rt, Vector2 position, Vector2 size)
    {
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
    }

    private static void StretchRuntimeRect(RectTransform rt, Vector2 offsetMin, Vector2 offsetMax)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
        rt.pivot = new Vector2(0.5f, 0.5f);
    }

    private static void SetObjectsActive(GameObject[] objects, bool active)
    {
        if (objects == null)
            return;

        for (int i = 0; i < objects.Length; i++)
        {
            if (objects[i] != null)
                objects[i].SetActive(active);
        }
    }

    private void SetAddress(string address, ushort targetPort)
    {
        serverIP = address;
        port = targetPort;
        SetDiagnosticTarget(serverIP, port, incrementAttempt: false);

        if (ipInput != null)
            ipInput.text = serverIP;

        SetStatus($"Target: {serverIP}:{port}");
    }

    private static void SetDiagnosticTarget(string address, ushort targetPort, bool incrementAttempt)
    {
        LastTargetAddress = address;
        LastTargetPort = targetPort;

        if (incrementAttempt)
            ConnectionAttemptId++;
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;
    }

    private void SetRoomCode(string message)
    {
        if (roomCodeText != null)
            roomCodeText.text = message;
    }

    private static T FindSceneComponentByName<T>(string objectName) where T : Component
    {
        T[] components = Resources.FindObjectsOfTypeAll<T>();
        for (int i = 0; i < components.Length; i++)
        {
            T component = components[i];
            if (component.gameObject.name == objectName && component.gameObject.scene.IsValid())
                return component;
        }

        return null;
    }

    private static GameObject FindSceneGameObjectByName(string objectName)
    {
        Transform[] transforms = Resources.FindObjectsOfTypeAll<Transform>();
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform transform = transforms[i];
            if (transform.gameObject.name == objectName && transform.gameObject.scene.IsValid())
                return transform.gameObject;
        }

        return null;
    }

    private void OnDestroy()
    {
        if (_subscribed && InstanceFinder.NetworkManager != null)
            InstanceFinder.ClientManager.OnClientConnectionState -= OnClientState;

        if (_roomResponseSubscribed && InstanceFinder.NetworkManager != null)
            InstanceFinder.ClientManager.UnregisterBroadcast<RoomAccessResponseBroadcast>(OnRoomAccessResponse);
    }
}
