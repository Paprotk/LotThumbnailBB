using System;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[BepInPlugin("Arro.LotThumbnailBB", "LotThumbnailBB", "1.0.0")]
public class LotThumbnailBB : BaseUnityPlugin
{
    void Awake()
    {
        var harmony = new Harmony("Arro.LotThumbnailBB");
        harmony.PatchAll();
        Logger.LogInfo("LotThumbnailBB Loaded");
    }
}

public static class ThumbnailHelper
{
    public static System.Collections.IEnumerator ApplyThumbnail(UICharacters uiChars)
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

    static System.Collections.IEnumerator RefreshAndCapture(int playerIndex, UICharacters uiChars)
    {
        var household = HouseholdManager.Instance?.CurrentHousehold;
        if (household == null || household.Data.OwnedLots.Count == 0) yield break;

        ulong lotGUID = household.Data.OwnedLots[0];
        Player player = PlayerManager.Instance.Players[playerIndex];

        ValueTuple<int, int, int> originalLayer = player.LotLayers.ContainsKey(lotGUID)
            ? player.LotLayers[lotGUID]
            : new ValueTuple<int, int, int>(0, 0, 0);

        if (player.LotLayers.ContainsKey(lotGUID))
        {
            var layers = player.LotLayers[lotGUID];
            player.LotLayers[lotGUID] = new ValueTuple<int, int, int>(
                int.MaxValue,
                layers.Item2,
                layers.Item3
            );
            BuildModeRefreshManager.IsFloorLayerVisibilityRefreshed = false;
        }
        
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();

        PremadeLotThumbnailManager.Instance.CreateRequest(lotGUID, delegate()
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