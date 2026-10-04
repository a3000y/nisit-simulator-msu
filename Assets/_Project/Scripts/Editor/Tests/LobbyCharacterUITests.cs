using NUnit.Framework;
using UnityEngine;
using NisitSimulator.Net;
using NisitSimulator.UI;

public class LobbyCharacterUITests
{
    [Test]
    public void SharedCharacterPageHasCompleteControlsAndOwnPreview()
    {
        var prefab = Resources.Load<GameObject>(LobbyCharacterCustomizer.ResourcePath);
        Assert.NotNull(prefab, "สร้างด้วย Nisit/Build Lobby Character UI ก่อน");
        var page = prefab.GetComponent<LobbyCharacterCustomizer>();
        Assert.NotNull(page);
        var creator = page.creator;
        Assert.NotNull(creator);
        Assert.AreEqual(2, creator.genderButtons.Length);
        Assert.Greater(creator.modelButtons.Length, 0);
        Assert.Greater(creator.colorButtons.Length, 0);
        Assert.Greater(creator.hairColorButtons.Length, 0);
        Assert.Greater(creator.skinButtons.Length, 0);
        Assert.AreEqual(4, creator.accessorySlots.Length);
        foreach (var slot in creator.accessorySlots) Assert.Greater(slot.buttons.Length, 0);
        Assert.NotNull(creator.randomButton);
        Assert.IsTrue(creator.previewCamera.transform.IsChildOf(prefab.transform));
        Assert.IsTrue(creator.previewRoot.IsChildOf(prefab.transform));
        Assert.IsTrue(creator.previewImage.transform.IsChildOf(prefab.transform));
        Assert.IsTrue(page.confirmButton.transform.IsChildOf(prefab.transform));
        Assert.IsTrue(page.backButton.transform.IsChildOf(prefab.transform));
    }

    [Test]
    public void SharedCharacterPageCannotStartSinglePlayerOrWriteSaveSlot()
    {
        var prefab = Resources.Load<GameObject>(LobbyCharacterCustomizer.ResourcePath);
        Assert.NotNull(prefab);
        Assert.IsNull(prefab.GetComponentInChildren<MainMenuController>(true));
        var page = prefab.GetComponent<LobbyCharacterCustomizer>();
        Assert.AreEqual(0, page.confirmButton.onClick.GetPersistentEventCount());
        Assert.AreEqual(0, page.backButton.onClick.GetPersistentEventCount());
        Assert.IsFalse(prefab.activeSelf, "เปิดเมื่อผู้เล่นกดแต่งตัวเท่านั้น");
    }
}
