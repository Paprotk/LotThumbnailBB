using System;
using System.Collections;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using Object = UnityEngine.Object;

[BepInPlugin("Arro.LotThumbnailBB", "LotThumbnailBB", "1.0.0")]
public class LotThumbnailBB : BaseUnityPlugin
{
    internal static LotThumbnailBB Instance { get; private set; }
    private UICharacters _uiChars;

    void Awake()
    {
        Instance = this;
        var harmony = new Harmony("Arro.LotThumbnailBB");
        harmony.PatchAll();
        Logger.LogInfo("LotThumbnailBB Loaded");
    }

    void Update()
    {
        if (!Input.GetMouseButtonDown(1)) return;
        if (_uiChars == null) return;

        var btn = _uiChars.CenterCameraToLotButton;
        if (btn == null || !btn.activeInHierarchy) return;
        
        var pointerData = new PointerEventData(EventSystem.current)
        {
            position = Input.mousePosition
        };
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, results);
        foreach (var result in results)
        {
            if (result.gameObject == btn || result.gameObject.transform.IsChildOf(btn.transform))
            {
                SaveCurrentViewAndCapture();
                return;
            }
        }
    }

    internal void SetUIChars(UICharacters uiChars)
    {
        _uiChars = uiChars;
    }

    internal void SaveCurrentViewAndCapture()
    {
        if (HouseholdManager.Instance == null || !HouseholdManager.Instance.HasCurrentHousehold)
            return;
        if (HouseholdManager.Instance.CurrentHousehold.Data.OwnedLots.Count == 0)
            return;

        ulong lotGUID = HouseholdManager.Instance.CurrentHousehold.Data.OwnedLots[0];
        var zone = ZoneManager.Instance.GetLotPerimeterZoneObjectByGUID(lotGUID);
        if (zone == null) return;

        var hybridPlayer = PlayerManager.Instance.GetHybridPlayer(0);
        if (hybridPlayer == null) return;
        Transform camT = hybridPlayer.HybridCamera.FreeCamera.CameraTransform.transform;

        zone.CameraPositionForThumbnail = camT.position;
        zone.CameraRotationForThumbnail = camT.rotation;

        Player player = hybridPlayer.Player;
        var originalLayer = player.LotLayers.TryGetValue(lotGUID, out var layer)
            ? layer
            : new ValueTuple<int, int, int>(0, 0, 0);

        if (player.LotLayers.ContainsKey(lotGUID))
        {
            var layers = player.LotLayers[lotGUID];
            player.LotLayers[lotGUID] = new ValueTuple<int, int, int>(
                int.MaxValue,
                layers.min,
                layers.max
            );
            BuildModeRefreshManager.IsFloorLayerVisibilityRefreshed = false;
        }

        StartCoroutine(CaptureAfterFrame(lotGUID, player, originalLayer));
    }

    private IEnumerator CaptureAfterFrame(
        ulong lotGUID, Player player, ValueTuple<int, int, int> originalLayer)
    {
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();

        PremadeLotThumbnailManager.Instance.CreateRequest(lotGUID, delegate
        {
            var lot = LotManager.Instance.GetLotByGUID(lotGUID);
            if (lot != null)
                lot.SaveThumbnailTexture(PremadeLotThumbnailManager.Instance.CurrentThumbnail);

            player.LotLayers[lotGUID] = originalLayer;
            BuildModeRefreshManager.IsFloorLayerVisibilityRefreshed = false;

            var uiChars = Object.FindObjectOfType<UICharacters>();
            if (uiChars != null)
                uiChars.StartCoroutine(ThumbnailHelper.ApplyThumbnail(uiChars));
        }, false);
    }
}

public static class ThumbnailHelper
{
    public static IEnumerator ApplyThumbnail(UICharacters uiChars)
    {
        yield return new WaitForSeconds(0.5f);

        if (uiChars == null) yield break;
        if (LotManager.Instance == null) yield break;
        if (HouseholdManager.Instance == null) yield break;

        var household = HouseholdManager.Instance.CurrentHousehold;
        if (household == null || household.Data.OwnedLots.Count == 0) yield break;

        var lot = LotManager.Instance.GetLotByGUID(household.Data.OwnedLots[0]);
        if (lot == null) yield break;

        var lotSprite = lot.ThumbnailSprite;
        if (lotSprite == null) yield break;

        var images = uiChars.ButtonBuildMode.GetComponentsInChildren<Image>();
        foreach (var img in images)
        {
            if (img.gameObject.name == "ImageHouseIcon")
            {
                img.sprite = lotSprite;
                img.color = Color.white;
                img.preserveAspect = false;

                var rect = img.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;

                var oldFitter = img.gameObject.GetComponent<AspectRatioFitter>();
                if (oldFitter != null) Object.DestroyImmediate(oldFitter);

                var fitter = img.gameObject.AddComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fitter.aspectRatio = lotSprite.texture.width / (float)lotSprite.texture.height;
            }
        }
    }
}

[HarmonyPatch(typeof(UICharacters), "OnShow")]
public class UICharactersPatch
{
    static void Postfix(UICharacters __instance)
    {
        if (LotThumbnailBB.Instance != null)
            LotThumbnailBB.Instance.SetUIChars(__instance);

        __instance.StartCoroutine(ThumbnailHelper.ApplyThumbnail(__instance));
    }
}

[HarmonyPatch(typeof(UILotInfo), "ChangeHousehold")]
public class HouseholdChangePatch
{
    static void Postfix()
    {
        var uiChars = Object.FindObjectOfType<UICharacters>();
        if (uiChars == null) return;
        uiChars.StartCoroutine(ThumbnailHelper.ApplyThumbnail(uiChars));
    }
}

[HarmonyPatch(typeof(SetPlayerLiveModeEvent), "UpdateMessage")]
public class SetPlayerLiveModePatch
{
    static void Postfix(MessageSetPlayerLiveMode message)
    {
        var uiChars = Object.FindObjectOfType<UICharacters>();
        if (uiChars == null) return;
        uiChars.StartCoroutine(RefreshAndCapture(message.PlayerIndex, uiChars));
    }

    static IEnumerator RefreshAndCapture(int playerIndex, UICharacters uiChars)
    {
        var household = HouseholdManager.Instance?.CurrentHousehold;
        if (household == null || household.Data.OwnedLots.Count == 0) yield break;

        ulong lotGUID = household.Data.OwnedLots[0];
        Player player = PlayerManager.Instance.Players[playerIndex];

        ValueTuple<int, int, int> originalLayer = player.LotLayers.TryGetValue(lotGUID, out var layer)
            ? layer
            : new ValueTuple<int, int, int>(0, 0, 0);

        if (player.LotLayers.ContainsKey(lotGUID))
        {
            var layers = player.LotLayers[lotGUID];
            player.LotLayers[lotGUID] = new ValueTuple<int, int, int>(
                int.MaxValue,
                layers.min,
                layers.max
            );
            BuildModeRefreshManager.IsFloorLayerVisibilityRefreshed = false;
        }

        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();

        PremadeLotThumbnailManager.Instance.CreateRequest(lotGUID, delegate
        {
            var lot = LotManager.Instance.GetLotByGUID(lotGUID);
            if (lot == null) return;
            lot.SaveThumbnailTexture(PremadeLotThumbnailManager.Instance.CurrentThumbnail);

            player.LotLayers[lotGUID] = originalLayer;
            BuildModeRefreshManager.IsFloorLayerVisibilityRefreshed = false;

            uiChars.StartCoroutine(ThumbnailHelper.ApplyThumbnail(uiChars));
        }, false);
    }
}