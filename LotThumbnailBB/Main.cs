using BepInEx;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

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