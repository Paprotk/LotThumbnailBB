using BepInEx;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

[BepInPlugin("com.arro.LotThumbnailBB", "LotThumbnailBB", "1.0.0")]
public class LotThumbnailButtonPlugin : BaseUnityPlugin
{
    void Awake()
    {
        var harmony = new Harmony("com.arro.LotThumbnailBB");
        harmony.PatchAll();
        Logger.LogInfo("LotThumbnailBB Loaded");
    }
}

[HarmonyPatch(typeof(UICharacters), "OnShow")]
public class UICharactersPatch
{
    static void Postfix(UICharacters __instance)
    {
        __instance.StartCoroutine(ApplyThumbnail(__instance));
    }

    static System.Collections.IEnumerator ApplyThumbnail(UICharacters uiChars)
    {
        yield return new WaitForSeconds(0.5f);

        if (LotManager.Instance == null || LotManager.Instance.Lots.Count == 0) yield break;

        Sprite lotSprite;
        var lotInfo = Object.FindObjectOfType<UILotInfo>();

        if (lotInfo != null && lotInfo.LotThumbnail.sprite != null)
            lotSprite = lotInfo.LotThumbnail.sprite;
        else
            lotSprite = LotManager.Instance.Lots[0].ThumbnailSprite;

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

                var fitter = img.gameObject.GetComponent<AspectRatioFitter>()
                          ?? img.gameObject.AddComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fitter.aspectRatio = lotSprite.texture.width / (float)lotSprite.texture.height;
            }
        }
    }
}