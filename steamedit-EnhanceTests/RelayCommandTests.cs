#nullable enable
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using steamedit_Enhance.ViewModels;

namespace steamedit_EnhanceTests;

[TestClass]
public class RelayCommandTests
{
    [TestMethod]
    public void Execute_Action_Invoked()
    {
        int x = 0;
        var cmd = new RelayCommand(() => x = 42);
        cmd.Execute(null);
        Assert.AreEqual(42, x);
    }

    [TestMethod]
    public void Execute_ActionWithParameter_Invoked()
    {
        string captured = "";
        var cmd = new RelayCommand(p => captured = (string?)p ?? "");
        cmd.Execute("hello");
        Assert.AreEqual("hello", captured);
    }

    [TestMethod]
    public void CanExecute_NoPredicate_AlwaysReturnsTrue()
    {
        var cmd = new RelayCommand(() => { });
        Assert.IsTrue(cmd.CanExecute(null));
        Assert.IsTrue(cmd.CanExecute("any"));
        Assert.IsTrue(cmd.CanExecute(42));
    }

    [TestMethod]
    public void CanExecute_WithPredicate_HonorsResult()
    {
        bool enabled = true;
        var cmd = new RelayCommand(() => { }, () => enabled);
        Assert.IsTrue(cmd.CanExecute(null));
        enabled = false;
        Assert.IsFalse(cmd.CanExecute(null));
    }

    [TestMethod]
    public void CanExecute_WithPredicateAndParam_HonorsResult()
    {
        var cmd = new RelayCommand(p => { }, p => p is string);
        Assert.IsTrue(cmd.CanExecute("foo"));
        Assert.IsFalse(cmd.CanExecute(null));
        Assert.IsFalse(cmd.CanExecute(42));
    }

    [TestMethod]
    public void RaiseCanExecuteChanged_FiresEvent()
    {
        var cmd = new RelayCommand(() => { });
        bool fired = false;
        cmd.CanExecuteChanged += (_, _) => fired = true;
        cmd.RaiseCanExecuteChanged();
        Assert.IsTrue(fired);
    }

    [TestMethod]
    public void RaiseCanExecuteChanged_MultipleSubscribers_AllFired()
    {
        var cmd = new RelayCommand(() => { });
        int count = 0;
        cmd.CanExecuteChanged += (_, _) => count++;
        cmd.CanExecuteChanged += (_, _) => count++;
        cmd.RaiseCanExecuteChanged();
        Assert.AreEqual(2, count);
    }

    [TestMethod]
    public void RaiseCanExecuteChanged_NoSubscribers_DoesNotThrow()
    {
        var cmd = new RelayCommand(() => { });
        cmd.RaiseCanExecuteChanged();
    }

    [TestMethod]
    public void Execute_MultipleCalls_InvokerMultipleTimes()
    {
        int count = 0;
        var cmd = new RelayCommand(() => count++);
        cmd.Execute(null);
        cmd.Execute(null);
        cmd.Execute(null);
        Assert.AreEqual(3, count);
    }

    [TestMethod]
    public void Command_MutatingState_ThenAskingCanExecute_NewPredicateReflected()
    {
        bool canRun = false;
        var cmd = new RelayCommand(() => { }, () => canRun);
        Assert.IsFalse(cmd.CanExecute(null));
        canRun = true;
        Assert.IsTrue(cmd.CanExecute(null));
    }
}
