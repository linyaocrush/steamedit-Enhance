#nullable enable
using System.Collections.Generic;
using System.ComponentModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using steamedit_Enhance.ViewModels;

namespace steamedit_EnhanceTests;

[TestClass]
public class ViewModelBaseTests
{
    private sealed class TestViewModel : ViewModelBase
    {
        private string _name = "initial";
        private int _value = 0;

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public int Value
        {
            get => _value;
            set => SetProperty(ref _value, value);
        }
    }

    [TestMethod]
    public void SetProperty_NewValue_RaisesPropertyChanged()
    {
        var vm = new TestViewModel();
        var fired = new List<string>();
        vm.PropertyChanged += (_, e) => fired.Add(e.PropertyName ?? "");

        vm.Name = "new";

        CollectionAssert.AreEqual(new[] { nameof(TestViewModel.Name) }, fired);
    }

    [TestMethod]
    public void SetProperty_SameValue_DoesNotRaise()
    {
        var vm = new TestViewModel();
        int count = 0;
        vm.PropertyChanged += (_, _) => count++;

        vm.Name = "initial";

        Assert.AreEqual(0, count);
    }

    [TestMethod]
    public void SetProperty_DirectSubclassCall_TracksChangeAndIdenticalValue()
    {
        var vm = new TestViewModel();
        var fired = new List<string>();
        vm.PropertyChanged += (_, e) => fired.Add(e.PropertyName ?? "");

        // 旧值 → 新值：变更并触发
        vm.Name = "changed";
        Assert.AreEqual(1, fired.Count);
        Assert.AreEqual(nameof(TestViewModel.Name), fired[0]);

        // 重复同值：不触发
        vm.Name = "changed";
        Assert.AreEqual(1, fired.Count);
    }

    [TestMethod]
    public void SetProperty_MultipleSubscribers_AllNotified()
    {
        var vm = new TestViewModel();
        int count = 0;
        PropertyChangedEventHandler h = (_, _) => count++;
        vm.PropertyChanged += h;
        vm.PropertyChanged += h;

        vm.Value = 1;

        Assert.AreEqual(2, count);
    }

    [TestMethod]
    public void SetProperty_DifferentProperties_OnlyCorrectOneRaised()
    {
        var vm = new TestViewModel();
        var fired = new List<string>();
        vm.PropertyChanged += (_, e) => fired.Add(e.PropertyName ?? "");

        vm.Value = 42;

        CollectionAssert.AreEqual(new[] { nameof(TestViewModel.Value) }, fired);
    }

    [TestMethod]
    public void Unsubscribe_StopsNotification()
    {
        var vm = new TestViewModel();
        int count = 0;
        PropertyChangedEventHandler h = (_, _) => count++;
        vm.PropertyChanged += h;
        vm.Value = 1;
        vm.PropertyChanged -= h;
        vm.Value = 2;

        Assert.AreEqual(1, count);
    }

    [TestMethod]
    public void Implements_INotifyPropertyChanged()
    {
        Assert.IsTrue(typeof(INotifyPropertyChanged).IsAssignableFrom(typeof(ViewModelBase)));
    }
}
