using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif
using UnityEngine.UI;

namespace CardGame32
{
    public class CardGamePrototypeController : MonoBehaviour
    {
        private enum RoundPhase
        {
            Betting,
            Challenge,
            Showdown,
            RoundEnd
        }

        [Header("Prototype")]
        [Range(4, 8)]
        public int playerCount = 6;

        public int startingScore = 50;
        public int roomNumber = 102938;

        private const int LocalPlayerIndex = 0;
        private const float TurnSeconds = 10f;
        private const string BackgroundResourcePath = "CardGame32/table-room-bg-v2";

        private readonly List<PlayerState> players = new List<PlayerState>();
        private readonly List<CardDefinition> deck = new List<CardDefinition>();
        private readonly List<GameObject> dealtObjects = new List<GameObject>();
        private readonly List<Text> playerScoreTexts = new List<Text>();
        private readonly List<Text> playerActionTexts = new List<Text>();
        private readonly List<Image> playerTurnRings = new List<Image>();
        private readonly LocalLobbyMockService lobbyService = new LocalLobbyMockService();

        private readonly Color coral = new Color(0.91f, 0.27f, 0.20f);
        private readonly Color teal = new Color(0.07f, 0.55f, 0.50f);
        private readonly Color green = new Color(0.18f, 0.55f, 0.31f);
        private readonly Color blue = new Color(0.12f, 0.38f, 0.64f);
        private readonly Color yellow = new Color(1.00f, 0.72f, 0.20f);
        private readonly Color cream = new Color(1.00f, 0.93f, 0.76f);
        private readonly Color navy = new Color(0.08f, 0.12f, 0.20f);
        private readonly Color gold = new Color(0.95f, 0.68f, 0.25f);
        private readonly Color panelDark = new Color(0.12f, 0.08f, 0.05f, 0.94f);
        private readonly Color panelRed = new Color(0.52f, 0.13f, 0.11f, 0.98f);

        private Font uiFont;
        private Camera mainCamera;
        private Canvas canvas;
        private Text potText;
        private Text statusText;
        private Text roomText;
        private Text chatPreviewText;
        private Text actionHintText;
        private Text actionTimerText;
        private Text microphoneButtonText;
        private InputField chatInputField;
        private Button foldButton;
        private Button raiseButton;
        private Button knockButton;
        private Transform backgroundRoot;
        private Transform tableOverlayRoot;
        private Transform cardRoot;
        private Transform playerRoot;
        private Transform hudRoot;
        private Transform actionRoot;
        private AccountProfile localAccount;
        private RoomSnapshot currentRoom;
        private VivoxRoomVoiceClient vivoxVoiceClient;
        private Sprite roundedSprite;
        private Sprite softRoundedSprite;
        private Sprite circleSprite;
        private Sprite cardSprite;
        private Sprite loadedBackgroundSprite;
        private RoundPhase phase;
        private int pot;
        private int currentTurnIndex;
        private int firstActorIndex;
        private int challengeLeaderIndex = -1;
        private int challengeCallerIndex = -1;
        private int roundNumber;
        private float turnTimeRemaining;
        private float aiThinkRemaining;
        private string statusMessage = string.Empty;
        private bool built;
        private bool microphoneMuted;
        private bool voiceJoinInProgress;
        private bool roundTransitionInProgress;

        private void Awake()
        {
            BuildPrototype();
        }

        public void BuildPrototypeForEditorPreview()
        {
            BuildPrototype();
        }

        private void BuildPrototype()
        {
            if (built)
            {
                return;
            }

            built = true;
            uiFont = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 32);
            vivoxVoiceClient = GetComponent<VivoxRoomVoiceClient>();
            if (vivoxVoiceClient == null)
            {
                vivoxVoiceClient = gameObject.AddComponent<VivoxRoomVoiceClient>();
            }

            BuildScene();
            StartNewMatch();
        }

        private void Update()
        {
            if (phase != RoundPhase.Betting && phase != RoundPhase.Challenge)
            {
                return;
            }

            if (roundTransitionInProgress || currentTurnIndex < 0 || currentTurnIndex >= players.Count)
            {
                return;
            }

            turnTimeRemaining -= Time.deltaTime;
            if (turnTimeRemaining <= 0f)
            {
                PlayerFold(currentTurnIndex, true);
                return;
            }

            if (currentTurnIndex != LocalPlayerIndex)
            {
                aiThinkRemaining -= Time.deltaTime;
                if (aiThinkRemaining <= 0f)
                {
                    PerformAiAction(currentTurnIndex);
                    return;
                }
            }

            UpdateActionControls();
        }

        public void StartNewMatch()
        {
            string[] names =
            {
                "\u6211\u81ea\u5df1",
                "\u751c\u5fc3\u5c0f\u7f8e",
                "\u5f00\u6717\u5c0f\u5b50",
                "\u9633\u5149\u963f\u6770",
                "\u53ef\u4e50\u59b9\u59b9",
                "\u5927\u529b\u963f\u798f",
                "\u4e91\u6735\u540c\u5b66",
                "\u5143\u6c14\u5c0f\u9732"
            };

            playerCount = Mathf.Clamp(playerCount, 4, 8);
            localAccount = new AccountProfile
            {
                AccountId = "local-player",
                DisplayName = names[0],
                ChipBalance = startingScore,
                CreatedAtUtc = System.DateTime.UtcNow.ToString("O"),
                LastLoginAtUtc = System.DateTime.UtcNow.ToString("O")
            };

            currentRoom = lobbyService.CreateRoom(localAccount, playerCount, true);
            roomNumber = int.Parse(currentRoom.Settings.RoomCode);

            players.Clear();
            for (int i = 0; i < playerCount; i++)
            {
                if (i > 0)
                {
                    lobbyService.JoinRoom(currentRoom.Settings.RoomCode, new AccountProfile
                    {
                        AccountId = "mock-player-" + i,
                        DisplayName = names[i],
                        ChipBalance = startingScore,
                        CreatedAtUtc = System.DateTime.UtcNow.ToString("O"),
                        LastLoginAtUtc = System.DateTime.UtcNow.ToString("O")
                    });
                }

                players.Add(new PlayerState
                {
                    Name = names[i],
                    Score = startingScore
                });
            }

            microphoneMuted = false;
            TryJoinVivoxRoomVoice();
            pot = 0;
            roundNumber = 0;
            RefillPotIfNeeded();
            DealRound();
            UpdateChatPreview();
        }

        public void DealRound()
        {
            StopAllCoroutines();
            roundTransitionInProgress = false;
            roundNumber++;
            challengeLeaderIndex = -1;
            challengeCallerIndex = -1;
            deck.Clear();
            deck.AddRange(CardGameRules.CreateShuffledDeck());

            foreach (PlayerState player in players)
            {
                player.Cards.Clear();
                player.Folded = false;
                player.Revealed = false;
                player.HasActed = false;
                player.LastAction = string.Empty;
            }

            for (int cardIndex = 0; cardIndex < 2; cardIndex++)
            {
                for (int i = 0; i < players.Count; i++)
                {
                    DrawCard(players[i]);
                }
            }

            RefillPotIfNeeded();
            firstActorIndex = Random.Range(0, playerCount);
            phase = RoundPhase.Betting;
            statusMessage = "\u7b2c " + roundNumber + " \u5c40\uff0c" + players[firstActorIndex].Name + " \u5148\u8bf4\u8bdd";
            RenderHands();
            StartTurn(firstActorIndex);
        }

        public void Fold()
        {
            if (!CanLocalAct())
            {
                return;
            }

            PlayerFold(LocalPlayerIndex, false);
        }

        public void Raise()
        {
            if (!CanLocalAct() || phase != RoundPhase.Betting)
            {
                return;
            }

            PlayerRaise(LocalPlayerIndex);
        }

        public void Knock()
        {
            if (!CanLocalAct())
            {
                return;
            }

            PlayerKnock(LocalPlayerIndex);
        }

        public void SendChatFromInput()
        {
            if (currentRoom == null || localAccount == null || chatInputField == null)
            {
                return;
            }

            string text = chatInputField.text.Trim();
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            lobbyService.SendTextMessage(currentRoom.Settings.RoomCode, localAccount, text);
            chatInputField.text = string.Empty;
            UpdateChatPreview();
        }

        public void ToggleMicrophone()
        {
            microphoneMuted = !microphoneMuted;
            if (currentRoom != null && localAccount != null)
            {
                lobbyService.SetMicrophoneMuted(currentRoom.Settings.RoomCode, localAccount.AccountId, microphoneMuted);
            }

            if (microphoneButtonText != null)
            {
                microphoneButtonText.text = microphoneMuted ? "\u9ea6\u5173" : "\u9ea6\u5f00";
            }

            if (vivoxVoiceClient == null)
            {
                return;
            }

            if (microphoneMuted)
            {
                vivoxVoiceClient.SetMicrophoneMuted(true);
            }
            else
            {
                TryJoinVivoxRoomVoice();
            }
        }

        private async void TryJoinVivoxRoomVoice()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (voiceJoinInProgress || currentRoom == null || localAccount == null || vivoxVoiceClient == null)
            {
                return;
            }

            voiceJoinInProgress = true;
            try
            {
                await vivoxVoiceClient.JoinRoomVoiceAsync(currentRoom.Settings.RoomCode, localAccount.DisplayName);
                vivoxVoiceClient.SetMicrophoneMuted(microphoneMuted);
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning("Vivox voice join failed: " + exception.Message);
            }
            finally
            {
                voiceJoinInProgress = false;
            }
        }

        private bool CanLocalAct()
        {
            if (currentTurnIndex != LocalPlayerIndex || roundTransitionInProgress)
            {
                return false;
            }

            if (phase != RoundPhase.Betting && phase != RoundPhase.Challenge)
            {
                return false;
            }

            return !players[LocalPlayerIndex].Folded;
        }

        private void StartTurn(int playerIndex)
        {
            currentTurnIndex = playerIndex;
            turnTimeRemaining = TurnSeconds;
            aiThinkRemaining = playerIndex == LocalPlayerIndex ? 0f : Random.Range(0.85f, 2.15f);

            if (phase == RoundPhase.Betting)
            {
                statusMessage = players[playerIndex].Name + " \u884c\u52a8\u4e2d\uff1a\u5f03\u724c / \u52a0\u6ce8 / \u6572\u684c";
            }
            else
            {
                string leaderName = challengeLeaderIndex >= 0 ? players[challengeLeaderIndex].Name : "\u5bf9\u624b";
                statusMessage = leaderName + " \u5df2\u6572\u684c\uff0c" + players[playerIndex].Name + " \u9009\u62e9\u6572\u684c\u6216\u5f03\u724c";
            }

            RefreshUi();
            UpdateActionControls();
        }

        private void PerformAiAction(int playerIndex)
        {
            if (playerIndex < 0 || playerIndex >= players.Count || players[playerIndex].Folded)
            {
                AdvanceAfterFoldOrAction(playerIndex);
                return;
            }

            HandEvaluation evaluation = CardGameRules.Evaluate(players[playerIndex].Cards[0], players[playerIndex].Cards[1]);
            int strength = (int)evaluation.Category;
            float roll = Random.value;

            if (phase == RoundPhase.Challenge)
            {
                if (strength >= (int)HandCategory.Special28 || roll > 0.58f)
                {
                    PlayerKnock(playerIndex);
                }
                else
                {
                    PlayerFold(playerIndex, false);
                }

                return;
            }

            if (strength >= (int)HandCategory.SpecialQ8 || roll > 0.84f)
            {
                PlayerKnock(playerIndex);
            }
            else if (players[playerIndex].Score > 0 && roll > 0.22f)
            {
                PlayerRaise(playerIndex);
            }
            else
            {
                PlayerFold(playerIndex, false);
            }
        }

        private void PlayerFold(int playerIndex, bool timedOut)
        {
            PlayerState player = players[playerIndex];
            player.Folded = true;
            player.Revealed = false;
            player.HasActed = true;
            player.LastAction = timedOut ? "\u8d85\u65f6\u5f03\u724c" : "\u5f03\u724c";
            statusMessage = player.Name + (timedOut ? " 10\u79d2\u672a\u9009\uff0c\u81ea\u52a8\u5f03\u724c" : " \u5f03\u724c");

            RenderHands();

            if (ActivePlayerCount() <= 1)
            {
                SettleSingleWinner();
                return;
            }

            AdvanceAfterFoldOrAction(playerIndex);
        }

        private void PlayerRaise(int playerIndex)
        {
            PlayerState player = players[playerIndex];
            if (player.Score > 0)
            {
                player.Score -= 1;
                pot += 1;
            }

            player.HasActed = true;
            player.LastAction = "\u52a0\u6ce8";
            statusMessage = player.Name + " \u52a0\u6ce8 1 \u5f20\u5e95\u724c";
            RenderHands();
            AdvanceAfterFoldOrAction(playerIndex);
        }

        private void PlayerKnock(int playerIndex)
        {
            PlayerState player = players[playerIndex];
            player.HasActed = true;
            player.LastAction = phase == RoundPhase.Challenge ? "\u8ddf\u6572" : "\u6572\u684c";

            if (phase == RoundPhase.Challenge)
            {
                challengeCallerIndex = playerIndex;
                StartShowdown(challengeLeaderIndex, challengeCallerIndex);
                return;
            }

            challengeLeaderIndex = playerIndex;
            phase = RoundPhase.Challenge;
            statusMessage = player.Name + " \u6572\u684c\uff0c\u5269\u4f59\u73a9\u5bb6\u53ef\u6572\u684c\u6216\u5f03\u724c";
            RenderHands();
            StartNextChallengeTurn(playerIndex);
        }

        private void AdvanceAfterFoldOrAction(int playerIndex)
        {
            if (roundTransitionInProgress)
            {
                return;
            }

            if (phase == RoundPhase.Challenge)
            {
                StartNextChallengeTurn(playerIndex);
                return;
            }

            int next = FindNextBettingPlayer(playerIndex);
            if (next >= 0)
            {
                StartTurn(next);
                return;
            }

            BeginChallengeAfterBetting();
        }

        private void BeginChallengeAfterBetting()
        {
            if (ActivePlayerCount() <= 1)
            {
                SettleSingleWinner();
                return;
            }

            challengeLeaderIndex = FindNextActivePlayer(firstActorIndex - 1, true);
            if (challengeLeaderIndex < 0)
            {
                SettleSingleWinner();
                return;
            }

            players[challengeLeaderIndex].LastAction = "\u7b49\u5f85\u5bf9\u624b";
            phase = RoundPhase.Challenge;
            statusMessage = "\u52a0\u6ce8\u8f6e\u7ed3\u675f\uff0c" + players[challengeLeaderIndex].Name + " \u7b49\u5f85\u5bf9\u624b\u6572\u684c";
            RenderHands();
            StartNextChallengeTurn(challengeLeaderIndex);
        }

        private void StartNextChallengeTurn(int afterIndex)
        {
            if (ActivePlayerCount() <= 1)
            {
                SettleSingleWinner();
                return;
            }

            int next = FindNextChallengePlayer(afterIndex);
            if (next >= 0)
            {
                StartTurn(next);
                return;
            }

            SettleSingleWinner();
        }

        private void StartShowdown(int firstIndex, int secondIndex)
        {
            if (firstIndex < 0 || secondIndex < 0)
            {
                SettleSingleWinner();
                return;
            }

            roundTransitionInProgress = true;
            phase = RoundPhase.Showdown;
            currentTurnIndex = -1;
            players[firstIndex].Revealed = true;
            players[secondIndex].Revealed = true;

            HandEvaluation first = CardGameRules.Evaluate(players[firstIndex].Cards[0], players[firstIndex].Cards[1]);
            HandEvaluation second = CardGameRules.Evaluate(players[secondIndex].Cards[0], players[secondIndex].Cards[1]);
            int winnerIndex = first.CompareTo(second) >= 0 ? firstIndex : secondIndex;
            int loserIndex = winnerIndex == firstIndex ? secondIndex : firstIndex;
            int sideLoss = Mathf.Min(players[loserIndex].Score, Mathf.Max(1, pot));

            players[loserIndex].Score -= sideLoss;
            players[winnerIndex].Score += pot + sideLoss;
            statusMessage = players[firstIndex].Name + " \u548c " + players[secondIndex].Name + " \u4eae\u724c\uff0c" + players[winnerIndex].Name + " \u8d62\u5f97\u5956\u6c60";
            pot = 0;
            RenderHands();
            RefreshUi();
            UpdateActionControls();
            StartCoroutine(NextRoundAfterDelay(3.2f));
        }

        private void SettleSingleWinner()
        {
            int winnerIndex = FindNextActivePlayer(-1, true);
            if (winnerIndex < 0)
            {
                DealRound();
                return;
            }

            roundTransitionInProgress = true;
            phase = RoundPhase.RoundEnd;
            currentTurnIndex = -1;
            players[winnerIndex].Score += pot;
            statusMessage = players[winnerIndex].Name + " \u7559\u5728\u724c\u5c40\u4e2d\uff0c\u62ff\u8d70\u5956\u6c60";
            pot = 0;
            RenderHands();
            RefreshUi();
            UpdateActionControls();
            StartCoroutine(NextRoundAfterDelay(2.2f));
        }

        private IEnumerator NextRoundAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            ClearDealtObjects();
            RefillPotIfNeeded();
            DealRound();
        }

        private int FindNextBettingPlayer(int afterIndex)
        {
            for (int offset = 1; offset <= players.Count; offset++)
            {
                int index = (afterIndex + offset + players.Count) % players.Count;
                if (!players[index].Folded && !players[index].HasActed)
                {
                    return index;
                }
            }

            return -1;
        }

        private int FindNextChallengePlayer(int afterIndex)
        {
            for (int offset = 1; offset <= players.Count; offset++)
            {
                int index = (afterIndex + offset + players.Count) % players.Count;
                if (index != challengeLeaderIndex && !players[index].Folded)
                {
                    return index;
                }
            }

            return -1;
        }

        private int FindNextActivePlayer(int afterIndex, bool includeAll)
        {
            for (int offset = 1; offset <= players.Count; offset++)
            {
                int index = (afterIndex + offset + players.Count) % players.Count;
                if (!players[index].Folded && (includeAll || index != challengeLeaderIndex))
                {
                    return index;
                }
            }

            return -1;
        }

        private int ActivePlayerCount()
        {
            int count = 0;
            for (int i = 0; i < players.Count; i++)
            {
                if (!players[i].Folded)
                {
                    count++;
                }
            }

            return count;
        }

        private void DrawCard(PlayerState player)
        {
            if (deck.Count == 0)
            {
                deck.AddRange(CardGameRules.CreateShuffledDeck());
            }

            CardDefinition card = deck[0];
            deck.RemoveAt(0);
            player.Cards.Add(card);
        }

        private void RefillPotIfNeeded()
        {
            int minimumPot = players.Count;
            if (pot >= minimumPot)
            {
                return;
            }

            for (int i = 0; i < players.Count; i++)
            {
                if (players[i].Score > 0)
                {
                    players[i].Score -= 1;
                    pot += 1;
                }
            }
        }

        private void BuildScene()
        {
            Application.targetFrameRate = 60;
            CreateCameraAndEventSystem();
            CreateCanvas();
        }

        private void CreateCameraAndEventSystem()
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                GameObject cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                mainCamera = cameraObject.AddComponent<Camera>();
            }

            mainCamera.orthographic = true;
            mainCamera.orthographicSize = 5f;
            mainCamera.transform.position = new Vector3(0f, 0f, -10f);
            mainCamera.transform.rotation = Quaternion.identity;
            mainCamera.nearClipPlane = 0.1f;
            mainCamera.farClipPlane = 100f;
            mainCamera.backgroundColor = navy;
            mainCamera.clearFlags = CameraClearFlags.SolidColor;

            if (FindAnyObjectByType<EventSystem>() == null)
            {
                GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
                eventSystem.AddComponent<InputSystemUIInputModule>();
#else
                eventSystem.AddComponent<StandaloneInputModule>();
#endif
                eventSystem.transform.SetParent(transform);
            }
        }

        private void CreateCanvas()
        {
            EnsureUiSprites();

            GameObject canvasObject = new GameObject("Mobile Game HUD");
            canvasObject.transform.SetParent(transform);
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = mainCamera;
            canvas.planeDistance = 1f;
            canvas.sortingOrder = 10;
            canvasObject.AddComponent<GraphicRaycaster>();

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(390f, 844f);
            scaler.matchWidthOrHeight = 1f;

            backgroundRoot = CreateLayer("Illustrated Background", canvas.transform);
            tableOverlayRoot = CreateLayer("Table Overlay", canvas.transform);
            cardRoot = CreateLayer("Cards UI", canvas.transform);
            playerRoot = CreateLayer("Player Badges UI", canvas.transform);
            hudRoot = CreateLayer("Top HUD UI", canvas.transform);
            actionRoot = CreateLayer("Action Bar UI", canvas.transform);

            CreateIllustratedBackground();
            CreateTableOverlay();
            CreateTopBar();
            CreatePotBadge();
            CreateSocialBar();
            CreatePlayerBadges();
            CreateStatusPanel();
            CreateActionBar();
        }

        private Transform CreateLayer(string objectName, Transform parent)
        {
            GameObject layer = new GameObject(objectName, typeof(RectTransform));
            layer.transform.SetParent(parent, false);
            Stretch((RectTransform)layer.transform, 0f, 0f, 0f, 0f);
            return layer.transform;
        }

        private void EnsureUiSprites()
        {
            if (roundedSprite != null)
            {
                return;
            }

            roundedSprite = CreateShapeSprite("Rounded Panel Sprite", 96, 18, false);
            softRoundedSprite = CreateShapeSprite("Soft Rounded Panel Sprite", 128, 38, false);
            cardSprite = CreateShapeSprite("Card Sprite", 96, 10, false);
            circleSprite = CreateShapeSprite("Circle Sprite", 128, 64, true);
        }

        private Sprite CreateShapeSprite(string spriteName, int size, int radius, bool circle)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = spriteName;
            texture.hideFlags = HideFlags.HideAndDontSave;

            float center = (size - 1) * 0.5f;
            float maxDistance = center - 1f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float alpha;
                    if (circle)
                    {
                        float distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                        alpha = Mathf.Clamp01(maxDistance - distance + 1f);
                    }
                    else
                    {
                        float dx = Mathf.Max(radius - x, x - (size - radius - 1), 0);
                        float dy = Mathf.Max(radius - y, y - (size - radius - 1), 0);
                        float distance = Mathf.Sqrt(dx * dx + dy * dy);
                        alpha = Mathf.Clamp01(radius - distance + 1f);
                    }

                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            Vector4 border = circle ? Vector4.zero : new Vector4(radius, radius, radius, radius);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
            sprite.name = spriteName;
            return sprite;
        }

        private void CreateIllustratedBackground()
        {
            Texture2D backgroundTexture = Resources.Load<Texture2D>(BackgroundResourcePath);
            Image background = CreatePanel("Painted Table Room", backgroundRoot, Color.white);
            Stretch(background.rectTransform, 0f, 0f, 0f, 0f);
            background.sprite = null;

            if (backgroundTexture != null)
            {
                loadedBackgroundSprite = Sprite.Create(
                    backgroundTexture,
                    new Rect(0f, 0f, backgroundTexture.width, backgroundTexture.height),
                    new Vector2(0.5f, 0.5f),
                    100f);
                background.sprite = loadedBackgroundSprite;
                background.type = Image.Type.Simple;
                background.color = Color.white;
            }
            else
            {
                background.color = new Color(0.14f, 0.18f, 0.27f, 1f);
                CreateFallbackRoom();
            }

            Image topShade = CreatePanel("Top Readability Shade", backgroundRoot, new Color(0f, 0f, 0f, 0.20f));
            topShade.sprite = null;
            SetAnchors(topShade.rectTransform, 0f, 0.76f, 1f, 1f);

            Image bottomShade = CreatePanel("Bottom Readability Shade", backgroundRoot, new Color(0f, 0f, 0f, 0.34f));
            bottomShade.sprite = null;
            SetAnchors(bottomShade.rectTransform, 0f, 0f, 1f, 0.24f);
        }

        private void CreateFallbackRoom()
        {
            Image wall = CreatePanel("Fallback Wall", backgroundRoot, new Color(0.13f, 0.18f, 0.27f, 1f));
            Stretch(wall.rectTransform, 0f, 0f, 0f, 0f);
            wall.sprite = null;

            Image table = CreateCircle("Fallback Table", backgroundRoot, teal);
            SetRect(table.rectTransform, new Vector2(0.5f, 0.48f), new Vector2(540f, 640f), Vector2.zero);
        }

        private void CreateTableOverlay()
        {
            Image centerGlow = CreateCircle("Center Prize Glow", tableOverlayRoot, new Color(1f, 0.82f, 0.34f, 0.10f));
            SetRect(centerGlow.rectTransform, new Vector2(0.5f, 0.50f), new Vector2(170f, 170f), Vector2.zero);

            Image cardSpot = CreatePanel("Local Card Spot", tableOverlayRoot, new Color(1f, 1f, 1f, 0.12f));
            cardSpot.sprite = softRoundedSprite;
            SetRect(cardSpot.rectTransform, new Vector2(0.5f, 0.335f), new Vector2(140f, 88f), Vector2.zero);
        }

        private void CreateTopBar()
        {
            Image left = CreateFramedPanel("Room Panel", hudRoot, panelDark, new Vector2(0.04f, 0.955f), new Vector2(0.315f, 0.993f), 3f);
            roomText = CreateText("Room Text", left.transform, string.Empty, 15, cream, TextAnchor.MiddleCenter);
            Stretch(roomText.rectTransform, 7f, 0f, -7f, 0f);

            Image right = CreateFramedPanel("Connection Panel", hudRoot, panelDark, new Vector2(0.705f, 0.955f), new Vector2(0.96f, 0.993f), 3f);
            Text connection = CreateText("Connection Text", right.transform, "Wi-Fi  20:30", 14, cream, TextAnchor.MiddleCenter);
            Stretch(connection.rectTransform, 6f, 0f, -6f, 0f);
        }

        private void CreatePotBadge()
        {
            Image panel = CreateFramedPanel("Prize Pool Badge", hudRoot, panelRed, new Vector2(0.325f, 0.868f), new Vector2(0.675f, 0.963f), 5f);
            panel.sprite = softRoundedSprite;

            Text label = CreateText("Prize Pool Label", panel.transform, "\u5956\u6c60", 18, Color.white, TextAnchor.MiddleCenter);
            label.fontStyle = FontStyle.Bold;
            SetAnchors(label.rectTransform, 0f, 0.62f, 1f, 0.96f);

            potText = CreateText("Prize Pool Amount", panel.transform, string.Empty, 36, yellow, TextAnchor.MiddleCenter);
            potText.fontStyle = FontStyle.Bold;
            SetAnchors(potText.rectTransform, 0f, 0.12f, 1f, 0.70f);
        }

        private void CreateSocialBar()
        {
            Image panel = CreateFramedPanel("Social Bar", hudRoot, new Color(0.07f, 0.07f, 0.10f, 0.92f), new Vector2(0.05f, 0.785f), new Vector2(0.95f, 0.846f), 3f);

            chatPreviewText = CreateText("Chat Preview", panel.transform, "\u804a\u5929\uff1a\u6b22\u8fce\u6765\u5230\u623f\u95f4", 13, cream, TextAnchor.MiddleLeft);
            SetAnchors(chatPreviewText.rectTransform, 0.035f, 0.54f, 0.56f, 0.94f);

            chatInputField = CreateInputField("Chat Input", panel.transform, new Vector2(0.035f, 0.08f), new Vector2(0.59f, 0.50f));
            CreateButton("Send Chat Button", panel.transform, "\u53d1\u9001", green, new Vector2(0.61f, 0.13f), new Vector2(0.76f, 0.86f), SendChatFromInput, false, 15);
            Button microphoneButton = CreateButton("Microphone Button", panel.transform, "\u9ea6\u5f00", blue, new Vector2(0.79f, 0.13f), new Vector2(0.96f, 0.86f), ToggleMicrophone, false, 15);
            microphoneButtonText = microphoneButton.GetComponentInChildren<Text>();
        }

        private InputField CreateInputField(string objectName, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            Image background = CreatePanel(objectName, parent, new Color(1f, 0.96f, 0.82f, 0.94f));
            SetAnchors(background.rectTransform, anchorMin.x, anchorMin.y, anchorMax.x, anchorMax.y);

            GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textObject.transform.SetParent(background.transform, false);
            Text text = textObject.GetComponent<Text>();
            text.font = uiFont;
            text.fontSize = 13;
            text.color = navy;
            text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            Stretch(text.rectTransform, 8f, 0f, -8f, 0f);

            GameObject placeholderObject = new GameObject("Placeholder", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            placeholderObject.transform.SetParent(background.transform, false);
            Text placeholder = placeholderObject.GetComponent<Text>();
            placeholder.font = uiFont;
            placeholder.fontSize = 13;
            placeholder.color = new Color(0.28f, 0.24f, 0.18f, 0.55f);
            placeholder.alignment = TextAnchor.MiddleLeft;
            placeholder.text = "\u8f93\u5165\u804a\u5929";
            Stretch(placeholder.rectTransform, 8f, 0f, -8f, 0f);

            InputField input = background.gameObject.AddComponent<InputField>();
            input.textComponent = text;
            input.placeholder = placeholder;
            input.characterLimit = 40;
            input.lineType = InputField.LineType.SingleLine;
            return input;
        }

        private void CreatePlayerBadges()
        {
            playerScoreTexts.Clear();
            playerActionTexts.Clear();
            playerTurnRings.Clear();
            string[] names =
            {
                "\u6211\u81ea\u5df1",
                "\u751c\u5fc3\u5c0f\u7f8e",
                "\u5f00\u6717\u5c0f\u5b50",
                "\u9633\u5149\u963f\u6770",
                "\u53ef\u4e50\u59b9\u59b9",
                "\u5927\u529b\u963f\u798f",
                "\u4e91\u6735\u540c\u5b66",
                "\u5143\u6c14\u5c0f\u9732"
            };

            Vector2[] anchors =
            {
                new Vector2(0.50f, 0.205f),
                new Vector2(0.155f, 0.570f),
                new Vector2(0.50f, 0.700f),
                new Vector2(0.845f, 0.570f),
                new Vector2(0.815f, 0.360f),
                new Vector2(0.185f, 0.360f),
                new Vector2(0.305f, 0.675f),
                new Vector2(0.695f, 0.675f)
            };

            Color[] avatarColors =
            {
                yellow,
                new Color(0.98f, 0.55f, 0.48f),
                new Color(0.45f, 0.72f, 0.92f),
                new Color(0.35f, 0.67f, 0.92f),
                new Color(0.93f, 0.52f, 0.65f),
                new Color(0.52f, 0.64f, 0.92f),
                new Color(0.50f, 0.78f, 0.68f),
                new Color(0.76f, 0.58f, 0.88f)
            };

            for (int i = 0; i < playerCount; i++)
            {
                Transform badge = CreatePlayerBadge("Player Badge " + (i + 1), names[i], startingScore.ToString("N0"), avatarColors[i], i == 0, i);
                RectTransform rect = (RectTransform)badge;
                rect.anchorMin = anchors[i];
                rect.anchorMax = anchors[i];
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(i == 0 ? 112f : 104f, i == 0 ? 92f : 86f);
                rect.anchoredPosition = Vector2.zero;
            }
        }

        private Transform CreatePlayerBadge(string objectName, string playerName, string scoreText, Color avatarColor, bool isSelf, int styleIndex)
        {
            GameObject root = new GameObject(objectName, typeof(RectTransform));
            root.transform.SetParent(playerRoot, false);

            Image turnRing = CreateCircle("Turn Ring", root.transform, new Color(1f, 0.88f, 0.30f, 0f));
            SetAnchors(turnRing.rectTransform, 0.19f, 0.37f, 0.81f, 1.02f);
            playerTurnRings.Add(turnRing);

            CreateCuteAvatar(root.transform, avatarColor, isSelf, styleIndex);

            Image labelPanel = CreateFramedPanel("Name Plate", root.transform, new Color(0.12f, 0.08f, 0.05f, 0.93f), new Vector2(0f, 0f), new Vector2(1f, 0.48f), 2f);

            Text name = CreateText("Name", labelPanel.transform, playerName, isSelf ? 15 : 13, Color.white, TextAnchor.MiddleCenter);
            name.fontStyle = FontStyle.Bold;
            SetAnchors(name.rectTransform, 0f, 0.48f, 1f, 0.98f);

            Text score = CreateText("Score", labelPanel.transform, "\u25ce " + scoreText, isSelf ? 15 : 14, yellow, TextAnchor.MiddleCenter);
            score.fontStyle = FontStyle.Bold;
            SetAnchors(score.rectTransform, 0f, 0.08f, 0.58f, 0.52f);
            playerScoreTexts.Add(score);

            Text action = CreateText("Action", labelPanel.transform, string.Empty, 12, cream, TextAnchor.MiddleCenter);
            SetAnchors(action.rectTransform, 0.50f, 0.06f, 1f, 0.52f);
            playerActionTexts.Add(action);

            return root.transform;
        }

        private void CreateCuteAvatar(Transform parent, Color baseColor, bool isSelf, int styleIndex)
        {
            Image ring = CreateCircle("Avatar Gold Ring", parent, gold);
            SetAnchors(ring.rectTransform, 0.22f, 0.38f, 0.78f, 1.00f);
            AddUiShadow(ring, new Vector2(0f, -3f), 0.28f);

            Image baseCircle = CreateCircle("Avatar Base", parent, baseColor);
            SetAnchors(baseCircle.rectTransform, 0.265f, 0.425f, 0.735f, 0.945f);

            Image face = CreateCircle("Face", parent, new Color(1.0f, 0.78f, 0.58f, 1f));
            SetAnchors(face.rectTransform, 0.35f, 0.50f, 0.65f, 0.84f);

            Image hair = CreatePanel("Hair", parent, isSelf ? new Color(0.15f, 0.09f, 0.05f, 1f) : new Color(0.18f, 0.10f, 0.06f, 1f));
            hair.sprite = softRoundedSprite;
            SetAnchors(hair.rectTransform, 0.33f, 0.74f, 0.67f, 0.88f);

            Image leftEye = CreateCircle("Left Eye", parent, navy);
            SetAnchors(leftEye.rectTransform, 0.415f, 0.64f, 0.452f, 0.676f);
            Image rightEye = CreateCircle("Right Eye", parent, navy);
            SetAnchors(rightEye.rectTransform, 0.548f, 0.64f, 0.585f, 0.676f);

            Image leftCheek = CreateCircle("Left Cheek", parent, new Color(1f, 0.35f, 0.36f, 0.38f));
            SetAnchors(leftCheek.rectTransform, 0.37f, 0.56f, 0.43f, 0.61f);
            Image rightCheek = CreateCircle("Right Cheek", parent, new Color(1f, 0.35f, 0.36f, 0.38f));
            SetAnchors(rightCheek.rectTransform, 0.57f, 0.56f, 0.63f, 0.61f);

            Text mouth = CreateText("Mouth", parent, "\u25e1", 17, navy, TextAnchor.MiddleCenter);
            SetAnchors(mouth.rectTransform, 0.43f, 0.50f, 0.57f, 0.59f);

            if (styleIndex % 4 == 1)
            {
                Image bowLeft = CreateCircle("Bow Left", parent, yellow);
                SetAnchors(bowLeft.rectTransform, 0.31f, 0.80f, 0.40f, 0.90f);
                Image bowRight = CreateCircle("Bow Right", parent, yellow);
                SetAnchors(bowRight.rectTransform, 0.38f, 0.80f, 0.47f, 0.90f);
            }
            else if (styleIndex % 4 == 2)
            {
                Image cap = CreatePanel("Cap", parent, coral);
                cap.sprite = softRoundedSprite;
                SetAnchors(cap.rectTransform, 0.31f, 0.78f, 0.69f, 0.89f);
            }
            else if (styleIndex % 4 == 3)
            {
                Image glassesLeft = CreatePanel("Glasses Left", parent, new Color(0.03f, 0.04f, 0.05f, 0.88f));
                SetAnchors(glassesLeft.rectTransform, 0.38f, 0.63f, 0.47f, 0.69f);
                Image glassesRight = CreatePanel("Glasses Right", parent, new Color(0.03f, 0.04f, 0.05f, 0.88f));
                SetAnchors(glassesRight.rectTransform, 0.53f, 0.63f, 0.62f, 0.69f);
            }
        }

        private void CreateStatusPanel()
        {
            Image panel = CreateFramedPanel("Status Panel", hudRoot, new Color(0.07f, 0.07f, 0.09f, 0.90f), new Vector2(0.045f, 0.145f), new Vector2(0.955f, 0.205f), 3f);
            statusText = CreateText("Status", panel.transform, "\u7b49\u5f85\u53d1\u724c", 14, cream, TextAnchor.MiddleCenter);
            Stretch(statusText.rectTransform, 10f, 0f, -10f, 0f);
        }

        private void CreateActionBar()
        {
            Image rail = CreatePanel("Action Rail", actionRoot, new Color(0.08f, 0.06f, 0.04f, 0.96f));
            SetAnchors(rail.rectTransform, 0f, 0f, 1f, 0.136f);
            rail.sprite = softRoundedSprite;
            AddUiShadow(rail, new Vector2(0f, 4f), 0.30f);

            actionHintText = CreateText("Action Hint", rail.transform, "\u8f6e\u5230\u4f60\u4e86", 13, cream, TextAnchor.MiddleCenter);
            SetAnchors(actionHintText.rectTransform, 0.03f, 0.70f, 0.52f, 0.98f);

            actionTimerText = CreateText("Action Timer", rail.transform, "10s", 18, yellow, TextAnchor.MiddleCenter);
            actionTimerText.fontStyle = FontStyle.Bold;
            SetAnchors(actionTimerText.rectTransform, 0.53f, 0.70f, 0.72f, 0.98f);

            foldButton = CreateButton("Fold Button", rail.transform, "\u5f03\u724c", coral, new Vector2(0.04f, 0.13f), new Vector2(0.28f, 0.70f), Fold, false, 24);
            raiseButton = CreateButton("Raise Button", rail.transform, "\u52a0\u6ce8", green, new Vector2(0.38f, 0.13f), new Vector2(0.62f, 0.70f), Raise, false, 24);
            knockButton = CreateButton("Knock Button", rail.transform, "\u6572\u684c", blue, new Vector2(0.72f, 0.13f), new Vector2(0.96f, 0.70f), Knock, false, 24);
        }

        private Button CreateButton(string objectName, Transform parent, string label, Color color, Vector2 anchorMin, Vector2 anchorMax, UnityEngine.Events.UnityAction action, bool round, int fontSize)
        {
            Image frame = round ? CreateCircle(objectName + " Frame", parent, gold) : CreatePanel(objectName + " Frame", parent, gold);
            SetAnchors(frame.rectTransform, anchorMin.x, anchorMin.y, anchorMax.x, anchorMax.y);
            AddUiShadow(frame, new Vector2(0f, -4f), 0.34f);

            Image fill = round ? CreateCircle(objectName, frame.transform, color) : CreatePanel(objectName, frame.transform, color);
            Stretch(fill.rectTransform, 4f, 4f, -4f, -4f);

            Image shine = CreatePanel(objectName + " Shine", fill.transform, new Color(1f, 1f, 1f, 0.16f));
            shine.sprite = round ? circleSprite : softRoundedSprite;
            SetAnchors(shine.rectTransform, 0.10f, 0.58f, 0.90f, 0.94f);

            Button button = frame.gameObject.AddComponent<Button>();
            button.targetGraphic = fill;
            button.onClick.AddListener(action);

            Text text = CreateText(objectName + " Text", fill.transform, label, fontSize, Color.white, TextAnchor.MiddleCenter);
            text.fontStyle = FontStyle.Bold;
            Stretch(text.rectTransform, 2f, 0f, -2f, 0f);

            return button;
        }

        private void RenderHands()
        {
            ClearDealtObjects();
            Vector2[] anchors =
            {
                new Vector2(0.50f, 0.333f),
                new Vector2(0.165f, 0.500f),
                new Vector2(0.50f, 0.635f),
                new Vector2(0.835f, 0.500f),
                new Vector2(0.770f, 0.290f),
                new Vector2(0.230f, 0.290f),
                new Vector2(0.315f, 0.620f),
                new Vector2(0.685f, 0.620f)
            };

            for (int i = 0; i < players.Count; i++)
            {
                if (players[i].Folded)
                {
                    continue;
                }

                bool reveal = i == LocalPlayerIndex || players[i].Revealed;
                Vector2 size = reveal ? new Vector2(56f, 80f) : new Vector2(39f, 55f);
                float spacing = reveal ? 32f : 22f;

                for (int c = 0; c < players[i].Cards.Count; c++)
                {
                    GameObject card = CreateCard(players[i].Cards[c], reveal);
                    card.transform.SetParent(cardRoot, false);
                    RectTransform rect = (RectTransform)card.transform;
                    SetRect(rect, anchors[i], size, new Vector2((c - 0.5f) * spacing, 0f));
                    rect.localEulerAngles = new Vector3(0f, 0f, (c - 0.5f) * (reveal ? -6f : 10f));
                    dealtObjects.Add(card);
                }
            }

            GameObject deckObject = CreateCard(default(CardDefinition), false);
            deckObject.name = "Deck Pile";
            deckObject.transform.SetParent(cardRoot, false);
            SetRect((RectTransform)deckObject.transform, new Vector2(0.5f, 0.410f), new Vector2(58f, 78f), Vector2.zero);
            dealtObjects.Add(deckObject);
        }

        private GameObject CreateCard(CardDefinition card, bool reveal)
        {
            GameObject cardObject = new GameObject(reveal ? card.DisplayName : "Face Down Card", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            Image frame = cardObject.GetComponent<Image>();
            frame.sprite = cardSprite;
            frame.type = Image.Type.Sliced;
            frame.color = reveal ? gold : new Color(0.88f, 0.84f, 0.72f, 1f);
            AddUiShadow(frame, new Vector2(0f, -3f), 0.30f);

            Image face = CreatePanel("Card Face", cardObject.transform, reveal ? Color.white : new Color(0.45f, 0.78f, 0.72f, 1f));
            face.sprite = cardSprite;
            Stretch(face.rectTransform, 3f, 3f, -3f, -3f);

            if (!reveal)
            {
                Image markCircle = CreateCircle("Back Medallion", face.transform, new Color(1f, 1f, 1f, 0.22f));
                SetRect(markCircle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(28f, 28f), Vector2.zero);
                Text backMark = CreateText("Back Mark", face.transform, "\u2663", 22, cream, TextAnchor.MiddleCenter);
                backMark.fontStyle = FontStyle.Bold;
                Stretch(backMark.rectTransform, 0f, 0f, 0f, 0f);
                return cardObject;
            }

            string suit = SuitSymbol(card.Suit);
            string rank = RankLabel(card.Rank);
            Color textColor = card.Color == CardColor.Red ? coral : navy;

            Text rankText = CreateText("Rank", face.transform, rank, 21, textColor, TextAnchor.UpperLeft);
            rankText.fontStyle = FontStyle.Bold;
            Stretch(rankText.rectTransform, 6f, 5f, -6f, -5f);

            Text suitText = CreateText("Suit", face.transform, suit, 29, textColor, TextAnchor.MiddleCenter);
            suitText.fontStyle = FontStyle.Bold;
            Stretch(suitText.rectTransform, 0f, 6f, 0f, -18f);

            return cardObject;
        }

        private string SuitSymbol(CardSuit suit)
        {
            switch (suit)
            {
                case CardSuit.Spades:
                    return "\u2660";
                case CardSuit.Hearts:
                    return "\u2665";
                case CardSuit.Diamonds:
                    return "\u2666";
                case CardSuit.Clubs:
                    return "\u2663";
                default:
                    return "\u738b";
            }
        }

        private string RankLabel(CardRank rank)
        {
            switch (rank)
            {
                case CardRank.Joker:
                    return "\u738b";
                case CardRank.Three:
                    return "3";
                case CardRank.Four:
                    return "4";
                case CardRank.Five:
                    return "5";
                case CardRank.Six:
                    return "6";
                case CardRank.Seven:
                    return "7";
                case CardRank.Eight:
                    return "8";
                case CardRank.Nine:
                    return "9";
                case CardRank.Ten:
                    return "10";
                case CardRank.Jack:
                    return "J";
                case CardRank.Queen:
                    return "Q";
                case CardRank.Two:
                    return "2";
                default:
                    return "?";
            }
        }

        private void RefreshUi()
        {
            if (potText != null)
            {
                potText.text = pot.ToString("N0");
            }

            if (roomText != null)
            {
                string code = currentRoom != null ? currentRoom.Settings.RoomCode : roomNumber.ToString();
                roomText.text = "\u623f\u53f7: " + code + "  " + players.Count + "/8";
            }

            if (statusText != null)
            {
                string hand = "\u7b49\u5f85\u53d1\u724c";
                if (players.Count > 0 && players[LocalPlayerIndex].Cards.Count >= 2)
                {
                    hand = players[LocalPlayerIndex].Folded
                        ? "\u4f60\u5df2\u5f03\u724c"
                        : CardGameRules.DescribeHand(players[LocalPlayerIndex].Cards[0], players[LocalPlayerIndex].Cards[1]);
                }

                statusText.text = statusMessage + " | " + hand;
            }

            for (int i = 0; i < playerScoreTexts.Count && i < players.Count; i++)
            {
                playerScoreTexts[i].text = "\u25ce " + players[i].Score.ToString("N0");
            }

            for (int i = 0; i < playerActionTexts.Count && i < players.Count; i++)
            {
                playerActionTexts[i].text = players[i].LastAction;
            }

            for (int i = 0; i < playerTurnRings.Count; i++)
            {
                Color color = gold;
                color.a = i == currentTurnIndex && (phase == RoundPhase.Betting || phase == RoundPhase.Challenge) ? 0.55f : 0f;
                playerTurnRings[i].color = color;
            }
        }

        private void UpdateActionControls()
        {
            bool localCanAct = CanLocalAct();
            if (actionRoot != null)
            {
                actionRoot.gameObject.SetActive(localCanAct);
            }

            if (!localCanAct)
            {
                RefreshUi();
                return;
            }

            if (actionTimerText != null)
            {
                actionTimerText.text = Mathf.CeilToInt(turnTimeRemaining).ToString() + "s";
            }

            if (actionHintText != null)
            {
                actionHintText.text = phase == RoundPhase.Betting
                    ? "\u8f6e\u5230\u4f60\u4e86\uff0c10\u79d2\u5185\u9009\u62e9"
                    : "\u5bf9\u624b\u6572\u684c\uff0c\u53ef\u6572\u684c\u6216\u5f03\u724c";
            }

            if (raiseButton != null)
            {
                raiseButton.gameObject.SetActive(phase == RoundPhase.Betting);
            }

            RefreshUi();
        }

        private void UpdateChatPreview()
        {
            if (chatPreviewText == null || currentRoom == null || currentRoom.RecentMessages.Count == 0)
            {
                return;
            }

            ChatMessage latest = currentRoom.RecentMessages[currentRoom.RecentMessages.Count - 1];
            chatPreviewText.text = latest.SenderName + "\uff1a" + latest.Text;
        }

        private void ClearDealtObjects()
        {
            for (int i = 0; i < dealtObjects.Count; i++)
            {
                if (dealtObjects[i] == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(dealtObjects[i]);
                }
                else
                {
                    DestroyImmediate(dealtObjects[i]);
                }
            }

            dealtObjects.Clear();
        }

        private Image CreateFramedPanel(string objectName, Transform parent, Color fillColor, Vector2 anchorMin, Vector2 anchorMax, float inset)
        {
            Image frame = CreatePanel(objectName + " Frame", parent, gold);
            frame.sprite = softRoundedSprite;
            SetAnchors(frame.rectTransform, anchorMin.x, anchorMin.y, anchorMax.x, anchorMax.y);
            AddUiShadow(frame, new Vector2(0f, -3f), 0.32f);

            Image fill = CreatePanel(objectName, frame.transform, fillColor);
            fill.sprite = softRoundedSprite;
            Stretch(fill.rectTransform, inset, inset, -inset, -inset);
            return fill;
        }

        private Image CreatePanel(string objectName, Transform parent, Color color)
        {
            GameObject panel = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panel.transform.SetParent(parent, false);
            Image image = panel.GetComponent<Image>();
            image.sprite = roundedSprite;
            image.type = Image.Type.Sliced;
            image.color = color;
            return image;
        }

        private Image CreateCircle(string objectName, Transform parent, Color color)
        {
            GameObject panel = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panel.transform.SetParent(parent, false);
            Image image = panel.GetComponent<Image>();
            image.sprite = circleSprite;
            image.type = Image.Type.Simple;
            image.color = color;
            return image;
        }

        private Text CreateText(string objectName, Transform parent, string text, int size, Color color, TextAnchor anchor)
        {
            GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textObject.transform.SetParent(parent, false);
            Text uiText = textObject.GetComponent<Text>();
            uiText.font = uiFont;
            uiText.text = text;
            uiText.fontSize = size;
            uiText.color = color;
            uiText.alignment = anchor;
            uiText.horizontalOverflow = HorizontalWrapMode.Wrap;
            uiText.verticalOverflow = VerticalWrapMode.Truncate;
            uiText.resizeTextForBestFit = true;
            uiText.resizeTextMinSize = Mathf.Max(10, size - 9);
            uiText.resizeTextMaxSize = size;

            Shadow shadow = textObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.50f);
            shadow.effectDistance = new Vector2(1.4f, -1.4f);
            return uiText;
        }

        private void AddUiShadow(Graphic graphic, Vector2 distance, float alpha)
        {
            Shadow shadow = graphic.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, alpha);
            shadow.effectDistance = distance;
        }

        private static void SetAnchors(RectTransform rect, float minX, float minY, float maxX, float maxY)
        {
            rect.anchorMin = new Vector2(minX, minY);
            rect.anchorMax = new Vector2(maxX, maxY);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SetRect(RectTransform rect, Vector2 anchor, Vector2 size, Vector2 position)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }

        private static void Stretch(RectTransform rect, float left, float bottom, float right, float top)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(right, top);
        }

        private class PlayerState
        {
            public string Name;
            public int Score;
            public bool Folded;
            public bool Revealed;
            public bool HasActed;
            public string LastAction = string.Empty;
            public readonly List<CardDefinition> Cards = new List<CardDefinition>();
        }
    }
}
