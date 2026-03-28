using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class GamblingMachineTests
{
    [TearDown]
    public void TearDown()
    {
        foreach (var gm in Object.FindObjectsByType<GameManager>(FindObjectsSortMode.None))
            Object.DestroyImmediate(gm.gameObject);

        foreach (var controller in Object.FindObjectsByType<GamblingMachineController>(FindObjectsSortMode.None))
            Object.DestroyImmediate(controller.gameObject);
    }

    [Test]
    public void TrySpin_ReturnsInsufficientFunds_WhenBalanceLowerThanBet()
    {
        var config = CreateConfig(victoryChance: 1f, minBet: 5f, maxBet: 100f);
        var gm = CreateGameManager(10f);
        var controller = CreateController(config);

        SpinResult result = controller.TrySpin(20f, forcedSeed: 123);

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(SpinFailureReason.InsufficientFunds, result.FailureReason);
        Assert.AreEqual(10f, gm.CasinoDeposit, 0.001f);
        Object.DestroyImmediate(config);
    }

    [Test]
    public void TrySpin_WinCase_PaysOutAndIncreasesBalance()
    {
        var config = CreateConfig(victoryChance: 1f, minBet: 5f, maxBet: 100f);
        var gm = CreateGameManager(100f);
        var controller = CreateController(config);

        SpinResult result = controller.TrySpin(10f, forcedSeed: 77);

        Assert.IsTrue(result.IsSuccess);
        Assert.IsTrue(result.IsWin);
        Assert.Greater(result.PayoutAmount, 0f);
        Assert.AreEqual(9, result.VisibleSymbols.Length);
        Assert.AreEqual(100f, result.BalanceBefore, 0.001f);
        Assert.Greater(result.BalanceAfter, 90f);
        Assert.AreEqual(result.BalanceAfter, gm.CasinoDeposit, 0.001f);
        Object.DestroyImmediate(config);
    }

    [Test]
    public void TrySpin_LoseCase_DeductsOnlyBet()
    {
        var config = CreateConfig(victoryChance: 0f, minBet: 5f, maxBet: 100f);
        var gm = CreateGameManager(100f);
        var controller = CreateController(config);

        SpinResult result = controller.TrySpin(10f, forcedSeed: 77);

        Assert.IsTrue(result.IsSuccess);
        Assert.IsFalse(result.IsWin);
        Assert.AreEqual(0f, result.PayoutAmount, 0.001f);
        Assert.AreEqual(90f, result.BalanceAfter, 0.001f);
        Assert.AreEqual(90f, gm.CasinoDeposit, 0.001f);
        Object.DestroyImmediate(config);
    }

    private static SlotMachineConfig CreateConfig(float victoryChance, float minBet, float maxBet)
    {
        var config = ScriptableObject.CreateInstance<SlotMachineConfig>();
        config.ReelCount = 3;
        config.RowCount = 3;
        config.MinBet = minBet;
        config.MaxBet = maxBet;
        config.VictoryChance = victoryChance;
        return config;
    }

    private static GameManager CreateGameManager(float casinoDeposit)
    {
        var go = new GameObject("GM_Test");
        var gm = go.AddComponent<GameManager>();
        InvokePrivate(gm, "Awake");
        gm.CasinoDeposit = casinoDeposit;
        return gm;
    }

    private static GamblingMachineController CreateController(SlotMachineConfig config)
    {
        var go = new GameObject("SlotController_Test");
        var controller = go.AddComponent<GamblingMachineController>();
        SetPrivateField(controller, "config", config);
        InvokePrivate(controller, "Awake");
        return controller;
    }

    private static void InvokePrivate(object target, string methodName)
    {
        var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        method?.Invoke(target, null);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        field?.SetValue(target, value);
    }
}
