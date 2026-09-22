using Il2Cpp;
using Il2CppKeepsake;
using Il2CppKeepsake.Modal;
using MelonLoader;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static Il2CppKeepsake.HyperSpace.NewInputSystem.InputManager;

[assembly: MelonInfo(typeof(ReGUI.Core), "ReGUI", "0.0.3", "GraciousCub5622", null)]
[assembly: MelonGame("Keepsake Games", "Jump Space")]

namespace ReGUI
{
    public class Core : MelonMod
    {
        public static bool captions = false;
        public static bool Controls = false;
        public static GameObject gui;
        public static GameObject lobbyHud;
        public static GameObject iconTracker;

        private static MelonPreferences_Category Prefs;

        private static MelonPreferences_Entry<string> IncreaseEquipmentKey;
        private static MelonPreferences_Entry<string> DecreaseEquipmentKey;
        private static MelonPreferences_Entry<float> EquipmentScale;

        private static MelonPreferences_Entry<string> IncreaseNotificationKey;
        private static MelonPreferences_Entry<string> DecreaseNotificationKey;
        private static MelonPreferences_Entry<float> NotificationScale;

        private static MelonPreferences_Entry<string> ToggleCaptionsKey;
        private static MelonPreferences_Entry<string> ToggleControlsKey;
        private static MelonPreferences_Entry<string> ToggleCrosshairKey;
        private static MelonPreferences_Entry<string> ToggleHudKey;

        private static MelonPreferences_Entry<string> ZoomKey;
        private static MelonPreferences_Entry<float> ZoomFOV;
        private static MelonPreferences_Entry<float> ZoomSpeed;

        private float originalFOV = -1f;

        private static MelonPreferences_Entry<string> OpenConfigKey;
        private static MelonPreferences_Entry<string> OpenZoomConfigKey;

        private bool waitingForKeybind;
        private MelonPreferences_Entry<string> keybindBeingSet;

        public override void OnInitializeMelon()
        {
            LoggerInstance.Msg("[ReGUI] Mod Initialized.");

            Prefs = MelonPreferences.CreateCategory("CompactUI");

            IncreaseEquipmentKey = Prefs.CreateEntry("IncreaseScaleKey", "F4");
            DecreaseEquipmentKey = Prefs.CreateEntry("DecreaseScaleKey", "F3");
            EquipmentScale = Prefs.CreateEntry("EquipmentBarScale", 1f);

            NotificationScale = Prefs.CreateEntry("NotificationScale", 1f);
            IncreaseNotificationKey = Prefs.CreateEntry("IncreaseNotificationKey", ",");
            DecreaseNotificationKey = Prefs.CreateEntry("DecreaseNotificationKey", ".");

            ToggleCaptionsKey = Prefs.CreateEntry("ToggleCaptionsKey", "F9");
            ToggleControlsKey = Prefs.CreateEntry("ToggleControlsKey", "F10");
            ToggleCrosshairKey = Prefs.CreateEntry("ToggleCrosshairKey", "F11");
            ToggleHudKey = Prefs.CreateEntry("ToggleHudKey", "F8");

            ZoomKey = Prefs.CreateEntry("ZoomKey", "Z");
            ZoomFOV = Prefs.CreateEntry("ZoomFOV", 35f);
            ZoomSpeed = Prefs.CreateEntry("ZoomSpeed", 10f);

            OpenConfigKey = Prefs.CreateEntry("OpenConfigMenuKey", "[");
            OpenZoomConfigKey = Prefs.CreateEntry("OpenZoomConfigMenuKey", "]");
        }

        public override void OnSceneWasInitialized(int buildIndex, string sceneName)
        {
            if (sceneName == "Global")
            {
                gui = GameObject.Find("PF_GUI/FullScreenGUI");
            }
            iconTracker = GameObject.Find("PF_GUI/FullScreenGUI/PF_IconTracker");
            MelonCoroutines.Start(LongDelayedActivate());

            if (sceneName == "Dest_Lobby_Start")
                lobbyHud = GameObject.Find("Gameplay/PF_Destination DestData_Lobby_Start/PF_LobbyHud");

            originalFOV = -1f;
        }

        public override void OnApplicationQuit()
        {
            MelonPreferences.Save();
        }

        public override void OnLateUpdate()
        {

            var kb = Keyboard.current;
            if (kb == null) return;

            try
            {
                if (IsKeyPressed(OpenConfigKey.Value, kb) && Global.m_GameSessionState.m_LocalPlayerIsPlaying.Value)
                {
                    ShowConfigMenu();
                }
            }
            catch { }

            try
            {
                if (IsKeyPressed(OpenZoomConfigKey.Value, kb) && Global.m_GameSessionState.m_LocalPlayerIsPlaying.Value)
                {
                    ShowZoomMenu();
                }
            }
            catch { }

            if (waitingForKeybind)
            {
                foreach (KeyControl kcode in Keyboard.current.allKeys)
                {
                    if (IsKeyPressed(kcode.displayName, kb))
                    {
                        keybindBeingSet.Value = kcode.displayName;
                        waitingForKeybind = false;
                        keybindBeingSet = null;
                        MelonPreferences.Save();
                        ModalManager.CancelAllModals();
                        MelonLogger.Msg($"[ReGUI] Bound to {kcode.displayName}");
                    }
                }
            }

            if (IsKeyPressed(ToggleCaptionsKey.Value, kb))
            {
                captions = !captions;
                MelonCoroutines.Start(ShortDelayedActivate());
            }

            if (IsKeyPressed(ToggleControlsKey.Value, kb))
            {
                Controls = !Controls;
                MelonCoroutines.Start(ShortDelayedActivate());
            }

            if (IsKeyPressed(ToggleCrosshairKey.Value, kb))
            {
                var crosshair = GameObject.Find("PF_GUI/FullScreenGUI/PF_FirstPersonHUD/Crosshairs");
                if (crosshair != null)
                    crosshair.SetActive(!crosshair.activeSelf);
            }

            if (IsKeyPressed(ToggleHudKey.Value, kb))
            {
                if (gui == null)
                    gui = GameObject.Find("PF_GUI/FullScreenGUI");

                if (lobbyHud == null)
                    lobbyHud = GameObject.Find("Gameplay/PF_Destination DestData_Lobby_Start/PF_LobbyHud");

                if (gui != null)
                    gui.SetActive(!gui.activeSelf);

                if (lobbyHud != null)
                    lobbyHud.SetActive(!lobbyHud.activeSelf);

                MelonCoroutines.Start(ShortDelayedActivate());
            }


            HandleScaleHotkeys();
            ApplyNotificationScale();
            try { HandleCameraZoom(kb); } catch { }
        }


        private void HandleCameraZoom(Keyboard kb)
        {
            if (Global.GameSettings.m_FovOnFoot_Setter != null && Global.GameSettings.m_FovOnFoot != null)
            {
                try
                {
                    if (originalFOV < 0f)
                        originalFOV = Global.GameSettings.m_FovOnFoot_Setter.Value;

                    bool zoomHeld = TryGetKey(ZoomKey.Value, kb, out var key) && key.isPressed;
                    float targetFOV = zoomHeld && !Cursor.visible ? ZoomFOV.Value : originalFOV;

                    if (!zoomHeld && Global.GameSettings.m_FovOnFoot.Value != originalFOV && Cursor.visible)
                    {
                        originalFOV = Global.GameSettings.m_FovOnFoot.Value;
                    }

                    Global.GameSettings.m_FovOnFoot_Setter.SetValue(Mathf.Lerp(
                        Global.GameSettings.m_FovOnFoot_Setter.Value,
                        targetFOV,
                        Time.deltaTime * ZoomSpeed.Value
                        )
                    );
                }
                catch { }
            }
        }

        private void ZoomFovModal()
        {
            var close = new ModalButton(
                "Close",
                InputKeys.Jump,
                onClick: new System.Action<ModalButton>((_) => ModalManager.CancelAllModals())
                );

            var increase = new ModalButton(
                $"Increase Zoom FOV",
                InputKeys.Jump,
                closeModalOnclick: false,
                onClick: new System.Action<ModalButton>((_) =>
                {
                    ZoomFOV.Value = Mathf.Clamp(ZoomFOV.Value + 5f, 5f, 110f);
                    MelonPreferences.Save();
                    ModalManager.CancelAllModals();
                    ZoomFovModal();
                })
            );

            var decrease = new ModalButton(
                $"Decrease Zoom FOV",
                InputKeys.Jump,
                closeModalOnclick: false,
                onClick: new System.Action<ModalButton>((_) =>
                {
                    ZoomFOV.Value = Mathf.Clamp(ZoomFOV.Value - 5f, 5f, 110f);
                    MelonPreferences.Save();
                    ModalManager.CancelAllModals();
                    ZoomFovModal();
                })
            );

            ModalManager.ShowModal(
                "Zoom FOV Adjustment",
                "Adjust the zoom field of view. Current : " + ZoomFOV.Value,
                InstigatorPriority.High,
                new System.Action<ModalRequest>((_) =>
                {
                    ModalManager.CancelModal(ModalManager.CurrentModal);
                }),
                increase,
                decrease,
                close
            );
        }

        private void ZoomSpeedModal()
        {
            var close = new ModalButton(
                "Close",
                InputKeys.Jump,
                onClick: new System.Action<ModalButton>((_) => ModalManager.CancelAllModals())
                );
            var increase = new ModalButton(
                $"Increase Zoom Speed",
                InputKeys.Jump,
                closeModalOnclick: false,
                onClick: new System.Action<ModalButton>((_) =>
                {
                    ZoomSpeed.Value = Mathf.Clamp(ZoomSpeed.Value + 1f, 1f, 20f);
                    MelonPreferences.Save();
                    ModalManager.CancelAllModals();
                    ZoomSpeedModal();
                })
            );
            var decrease = new ModalButton(
                $"Decrease Zoom Speed",
                InputKeys.Jump,
                closeModalOnclick: false,
                onClick: new System.Action<ModalButton>((_) =>
                {
                    ZoomSpeed.Value = Mathf.Clamp(ZoomSpeed.Value - 1f, 1f, 20f);
                    MelonPreferences.Save();
                    ModalManager.CancelAllModals();
                    ZoomSpeedModal();
                })
            );
            ModalManager.ShowModal(
                "Zoom Speed Adjustment",
                "Adjust the zoom speed. Current : " + ZoomSpeed.Value,
                InstigatorPriority.High,
                new System.Action<ModalRequest>((_) =>
                {
                    ModalManager.CancelModal(ModalManager.CurrentModal);
                }),
                increase,
                decrease,
                close
            );
        }

        private void ShowZoomMenu()
        {
            var close = new ModalButton(
                "Close",
                InputKeys.Jump,
                onClick: new System.Action<ModalButton>((_) => ModalManager.CancelAllModals())
                );

            var zoomKey = new ModalButton(
                $"Zoom Key: {ZoomKey.Value}",
                InputKeys.Jump,
                onClick: new System.Action<ModalButton>((_) =>
                {
                    BeginKeyRebind(ZoomKey);
                })
            );

            var zoomSpeedSetting = new ModalButton(
                $"Zoom Speed: {ZoomSpeed.Value}",
                InputKeys.Jump,

                onClick: new System.Action<ModalButton>((_) =>
                {
                    ZoomSpeedModal();
                })
            );

            var zoomSetting = new ModalButton(
                $"Zoom FOV: {ZoomFOV.Value}",
                InputKeys.Jump,
                onClick: new System.Action<ModalButton>((_) =>
                {
                    ZoomFovModal();
                })
            );

            ModalManager.ShowModal(
                "Zoom Configuration",
                "Adjust zoom settings here.",
                InstigatorPriority.High,
                new System.Action<ModalRequest>((_) =>
                {
                    ModalManager.CancelModal(ModalManager.CurrentModal);
                }),
                zoomKey,
                zoomSpeedSetting,
                zoomSetting,
                close
            );
        }

        private void ShowConfigMenu()
        {
            var close = new ModalButton(
                "Close",
                InputKeys.Jump,
                onClick: new System.Action<ModalButton>((_) => ModalManager.CancelAllModals())
                );

            var incScale = new ModalButton(
                $"Increase Scale: {IncreaseEquipmentKey.Value}",
                InputKeys.Jump,
                onClick: new System.Action<ModalButton>((_) => BeginKeyRebind(IncreaseEquipmentKey))
            );

            var decScale = new ModalButton(
                $"Decrease Scale: {DecreaseEquipmentKey.Value}",
                InputKeys.Jump,
                onClick: new System.Action<ModalButton>((_) => BeginKeyRebind(DecreaseEquipmentKey))
            );

            var toggleCaptions = new ModalButton(
                $"Toggle Captions: {ToggleCaptionsKey.Value}",
                InputKeys.Jump,
                onClick: new System.Action<ModalButton>((_) => BeginKeyRebind(ToggleCaptionsKey))
            );

            var toggleControls = new ModalButton(
                $"Toggle Controls: {ToggleControlsKey.Value}",
                InputKeys.Jump,
                onClick: new System.Action<ModalButton>((_) => BeginKeyRebind(ToggleControlsKey))
            );

            var toggleCrosshair = new ModalButton(
                $"Toggle Crosshair: {ToggleCrosshairKey.Value}",
                InputKeys.Jump,
                onClick: new System.Action<ModalButton>((_) => BeginKeyRebind(ToggleCrosshairKey))
            );

            var toggleHud = new ModalButton(
                $"Toggle HUD: {ToggleHudKey.Value}",
                InputKeys.Jump,
                onClick: new System.Action<ModalButton>((_) => BeginKeyRebind(ToggleHudKey))
            );

            var zoomKey = new ModalButton(
                $"Zoom Key: {ZoomKey.Value}",
                InputKeys.Jump,
                onClick: new System.Action<ModalButton>((_) => BeginKeyRebind(ZoomKey))
            );

            ModalManager.ShowModal(
                "ReGUI Configuration",
                "Rebind keys or adjust values.\nClick a bind, then press a key.",
                InstigatorPriority.High,
                new System.Action<ModalRequest>((_) =>
                {
                    ModalManager.CancelModal(ModalManager.CurrentModal);
                }),
                incScale,
                decScale,
                toggleCaptions,
                toggleControls,
                toggleCrosshair,
                toggleHud,
                close
            );
        }

        private void BeginKeyRebind(MelonPreferences_Entry<string> entry)
        {
            waitingForKeybind = true;
            keybindBeingSet = entry;

            ModalManager.ShowModal(
                "Rebind Key",
                $"Press any key to bind:\n{entry.Identifier}",
                InstigatorPriority.High,
                new System.Action<ModalRequest>((_) =>
                {
                    waitingForKeybind = false;
                    keybindBeingSet = null;
                })
            );
        }

        private void HandleScaleHotkeys()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            if (TryGetKey(IncreaseEquipmentKey.Value, kb, out var incKey) && incKey.wasPressedThisFrame)
            {
                EquipmentScale.Value = Mathf.Clamp(EquipmentScale.Value + 0.05f, 0.2f, 2.0f);
                ApplyEquipmentScale();
                LoggerInstance.Msg($"[CompactUI] Equipment bar scale: {EquipmentScale.Value:F2}");
                MelonPreferences.Save();
            }

            if (TryGetKey(DecreaseEquipmentKey.Value, kb, out var decKey) && decKey.wasPressedThisFrame)
            {
                EquipmentScale.Value = Mathf.Clamp(EquipmentScale.Value - 0.05f, 0.2f, 2.0f);
                ApplyEquipmentScale();
                LoggerInstance.Msg($"[CompactUI] Equipment bar scale: {EquipmentScale.Value:F2}");
                MelonPreferences.Save();
            }

            if (TryGetKey(IncreaseNotificationKey.Value, kb, out var incNotificationKey) && incNotificationKey.wasPressedThisFrame)
            {
                NotificationScale.Value = Mathf.Clamp(NotificationScale.Value + 0.05f, 0.2f, 2.0f);
                LoggerInstance.Msg($"[CompactUI] Notification bar scale: {NotificationScale.Value:F2}");
                MelonPreferences.Save();
            }

            if (TryGetKey(DecreaseNotificationKey.Value, kb, out var decNotificationKey) && decNotificationKey.wasPressedThisFrame)
            {
                NotificationScale.Value = Mathf.Clamp(NotificationScale.Value - 0.05f, 0.2f, 2.0f);
                LoggerInstance.Msg($"[CompactUI] Notification bar scale: {NotificationScale.Value:F2}");
                MelonPreferences.Save();
            }
        }

        private static bool IsKeyPressed(string keyName, Keyboard kb)
        {
            return TryGetKey(keyName, kb, out var key) && key.wasPressedThisFrame;
        }

        private static bool TryGetKey(string keyName, Keyboard kb, out KeyControl key)
        {
            key = kb.FindKeyOnCurrentKeyboardLayout(keyName);
            return key != null;
        }

        private IEnumerator LongDelayedActivate()
        {
            yield return new WaitForSeconds(5f);
            changeUI();
        }

        private IEnumerator ShortDelayedActivate()
        {
            yield return new WaitForSeconds(0.3f);
            changeUI();
        }

        public static void changeUI()
        {
            MelonPreferences.Load();

            try { ApplyEquipmentScale(); } catch { }
            try { ApplyNotificationScale(); } catch { }

            try
            {
                GameObject.Find("PF_GUI/FullScreenGUI/PF_FirstPersonHUD/PlayerStatusBar/PlayerBar")
                    .GetComponent<Image>().enabled = false;
            }
            catch { }

            try
            {
                GameObject.Find("PF_GUI/FullScreenGUI/PF_FirstPersonHUD/PlayerStatusBar/PlayerBar/LocationLabel")
                    .SetActive(false);
            }
            catch { }

            try
            {
                GameObject.Find("PF_GUI/FullScreenGUI/PF_FirstPersonHUD/PlayerStatusBar/PlayerBar/PlayernameLabel")
                    .SetActive(false);
            }
            catch { }

            try
            {
                GameObject.Find("PF_GUI/FullScreenGUI/PF_Hints")
                    .SetActive(captions);
            }
            catch { }

            try
            {
                GameObject.Find("Gameplay/PF_Destination DestData_Lobby_Start/PF_LobbyHud/PF_PlayerStatusBar_Lobby/PlayerBar/PlayernameLabel")
                    .SetActive(false);
            }
            catch { }

            try
            {
                GameObject.Find("Gameplay/PF_Destination DestData_Lobby_Start/PF_LobbyHud/PF_PlayerGroupBar_Lobby/InviteFriendBox")
                    .SetActive(false);
            }
            catch { }

            try
            {
                GameObject.Find("Gameplay/PF_Destination DestData_Lobby_Start/PF_LobbyHud/PF_PlayerStatusBar_Lobby/PF_Wallet/CreditsBox")
                    .transform.localPosition = new Vector3(165f, -55f, 0f);
            }
            catch { }

            try
            {
                GameObject.Find("Gameplay/PF_Destination DestData_Lobby_Start/PF_LobbyHud/PF_PlayerStatusBar_Lobby/PF_Wallet/IngotsBox")
                    .transform.localPosition = new Vector3(320f, -55f, 0f);
            }
            catch { }

            try
            {
                var controls = FindGameObjectsContaining("PF_IngameFullscreen");
                foreach (var control in controls)
                    control.SetActive(!Controls);
            }
            catch { }

            try
            {
                var controls = FindGameObjectsContaining("PF_IngameFullscreen");
                foreach (var control in controls)
                    control.SetActive(!Controls);
            }
            catch { }
        }

        private static void ApplyEquipmentScale()
        {
            var bar = GameObject.Find("PF_GUI/FullScreenGUI/PF_FirstPersonHUD/PF_EquipmentBar");
            if (bar != null)
            {
                float s = EquipmentScale.Value;
                bar.transform.localScale = new Vector3(s, s, s);
            }
        }

        private static void ApplyNotificationScale()
        {
            if (iconTracker == null)
            {
                return;
            }
            foreach (var icon in GetDirectChilderen(iconTracker))
            {
                if (icon != null)
                {
                    float s = NotificationScale.Value;
                    icon.transform.localScale = new Vector3(s, s, s);
                }
            }
        }

        public static List<GameObject> GetDirectChilderen(GameObject parent)
        {
            List<GameObject> descendants = new List<GameObject>();

            // GetComponentsInChildren returns parent + all children
            Transform[] allTransforms = parent.GetComponentsInChildren<Transform>(true);

            foreach (Transform t in allTransforms)
            {
                // Skip the parent object itself
                if (t.gameObject != parent)
                {
                    descendants.Add(t.gameObject);
                }
            }

            return descendants;
        }

        public static List<GameObject> FindGameObjectsContaining(string substring)
        {
            List<GameObject> matches = new();
            foreach (var obj in GameObject.FindObjectsOfType<GameObject>())
                if (obj?.name?.Contains(substring) == true)
                    matches.Add(obj);
            return matches;
        }
    }
}
