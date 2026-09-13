using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using HMUI;
using UnityEngine;

// Native UI/transition doubles execute the product menu, flow, view and session code.
// Dismissal is deliberately deferred; this does not simulate BSML layout or Unity rendering.
namespace UnityEngine
{
    public static partial class Application { public static string persistentDataPath; }
    public static class Time { public static int frameCount; public static float unscaledTime; }
    public struct Color
    {
        public float r, g, b, a;
        public Color(float red, float green, float blue, float alpha = 1) { r = red; g = green; b = blue; a = alpha; }
        public static Color white => new Color(1, 1, 1);
    }
}
namespace TMPro
{ public class TextMeshProUGUI : MonoBehaviour { public string text; public Color color = Color.white; } }
namespace UnityEngine.SceneManagement
{
    public struct Scene { public int handle; public bool isLoaded; }
    public static class SceneManager
    {
        public static event Action<Scene, Scene> activeSceneChanged;
        public static int sceneCount => 0;
        public static Scene GetSceneAt(int index) => default;
        public static void Change() => activeSceneChanged?.Invoke(default, default);
    }
}
namespace Zenject
{
    [AttributeUsage(AttributeTargets.Constructor)]
    public sealed class InjectAttribute : Attribute { }
    public interface IInitializable { void Initialize(); }
    public interface ITickable { void Tick(); }
}
namespace IPA.Utilities { public static class UnityGame { public static string UserDataPath; } }
namespace IPA.Config.Stores
{
    public static class GeneratedStore { public const string AssemblyVisibilityTarget = "IPA.Config.Stores.Generated"; }
}
namespace IPA.Config.Stores.Attributes
{
    public sealed class NonNullableAttribute : Attribute { }
    public sealed class UseConverterAttribute : Attribute { public UseConverterAttribute(Type type) { } }
}
namespace IPA.Config.Stores.Converters { public sealed class ListConverter<T> { } }
namespace MovementRecorder
{
    internal static class Plugin { public static TestLog Log = new TestLog(); }
    internal sealed class TestLog
    {
        public readonly List<string> Errors = new List<string>();
        public void Warn(string message) { }
        public void Debug(string message) { }
        public void Info(string message) { }
        public void Error(string message) { Errors.Add(message); }
    }
}
namespace MovementRecorder.Models
{
    internal sealed class RecordData
    {
        public event Action<string> FileSaved;
        public Task _saveTask;
        public object[] _recordData = null;
        public int _transformSize = 0;
        public void Saved(string path) => FileSaved?.Invoke(path);
    }
}
namespace MovementRecorder.Playback.Compatibility
{ internal sealed class ReplaySaveGuards { public void Prepare() { } } }
namespace MovementRecorder.Playback.Runtime
{ internal static class ReplayRuntimeHooks { public static void Prepare() { } } }
namespace SiraUtil.Submissions
{
    public sealed class Ticket { }
    public sealed class Submission
    { public Ticket DisableScoreSubmission(string mod, string reason) => new Ticket(); public void Remove(Ticket ticket) { } }
}

public enum BeatmapDifficulty { Easy, Normal, Hard, Expert, ExpertPlus }
public interface IDifficultyBeatmap
{
    TestBeatmapLevel level { get; }
    TestBeatmapSet parentDifficultyBeatmapSet { get; }
    BeatmapDifficulty difficulty { get; }
}
public class TestBeatmapLevel { public string levelID, songName; }
public class CustomPreviewBeatmapLevel : TestBeatmapLevel { public string customLevelPath; }
public sealed class TestBeatmapSet { public TestCharacteristic beatmapCharacteristic = new TestCharacteristic(); }
public sealed class TestCharacteristic { public string serializedName = "Standard"; }
public sealed class TestBeatmap : IDifficultyBeatmap
{
    public TestBeatmapLevel level { get; set; } = new TestBeatmapLevel { levelID = "song", songName = "Test song" };
    public TestBeatmapSet parentDifficultyBeatmapSet { get; set; } = new TestBeatmapSet();
    public BeatmapDifficulty difficulty { get; set; } = BeatmapDifficulty.Expert;
}
public sealed class StandardLevelDetailViewController : MonoBehaviour { public IDifficultyBeatmap selectedDifficultyBeatmap; }
public sealed class GameplayModifiers
{
    public enum SongSpeed { Normal }
    public GameplayModifiers CopyWith(bool noFailOn0Energy, SongSpeed songSpeed) => this;
}
public sealed class TestPlayerSettings { public TestPlayerSettings CopyWith(bool autoRestart) => this; }
public sealed class TestColorSettings { public object GetOverrideColorScheme() => null; }
public sealed class GameplaySetupViewController
{
    public GameplayModifiers gameplayModifiers = new GameplayModifiers();
    public object environmentOverrideSettings;
    public TestColorSettings colorSchemesSettings = new TestColorSettings();
    public TestPlayerSettings playerSettings = new TestPlayerSettings();
}
public sealed class PracticeSettings { public float startSongTime, songSpeedMul; }
public sealed class MenuTransitionsHelper
{
    public int Starts;
    public IDifficultyBeatmap StartedChart;
    public void StartStandardLevel(string mode, IDifficultyBeatmap chart, TestBeatmapLevel level, object environment, object color,
        GameplayModifiers modifiers, TestPlayerSettings settings, PracticeSettings practice, string back, bool a, bool b,
        object before, Action<object, object> finished, object after) { Starts++; StartedChart = chart; }
}
namespace SongCore
{
    public sealed class TestRequirements { public string[] _requirements; }
    public sealed class TestDifficultyData { public TestRequirements additionalDifficultyData; }
    public static class Collections { public static TestDifficultyData RetrieveDifficultyData(IDifficultyBeatmap chart) => null; }
}
namespace HMUI
{
    public class ViewController : MonoBehaviour { }
    public class FlowCoordinator : MonoBehaviour
    {
        private bool _activated;
        protected bool showBackButton;
        public string Title;
        protected void SetTitle(string title) { Title = title; }
        protected void ProvideInitialViewControllers(ViewController view) { }
        public FlowCoordinator YoungestChildFlowCoordinatorOrSelf() => this;
        protected virtual void DidActivate(bool first, bool added, bool enabling) { }
        protected virtual void BackButtonWasPressed(ViewController view) { }
        public void ActivateFlow() { DidActivate(!_activated, true, true); _activated = true; }
        public void Back() => BackButtonWasPressed(null);
    }
    public sealed class TableView
    {
        public int Selected = -1, Reloads;
        public void ReloadData() { Reloads++; Selected = -1; }
        public void SelectCellWithIdx(int index, bool callback) { Selected = index; }
        public void ClearSelection() { Selected = -1; }
    }
}
namespace BeatSaberMarkupLanguage.Attributes
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public sealed class UIValueAttribute : Attribute { public UIValueAttribute(string name) { } }
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class UIActionAttribute : Attribute { public UIActionAttribute(string name) { } }
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class UIComponentAttribute : Attribute { public UIComponentAttribute(string name) { } }
}
namespace BeatSaberMarkupLanguage.Components
{ public sealed class CustomCellListTableData { public TableView tableView = new TableView(); } }
namespace BeatSaberMarkupLanguage.ViewControllers
{
    public class BSMLAutomaticViewController : ViewController, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected void NotifyPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        protected virtual void OnDestroy() { }
    }
}
namespace BeatSaberMarkupLanguage
{
    public static class BeatSaberUI
    {
        public static FlowCoordinator MainFlowCoordinator;
        public static FlowCoordinator Presented;
        public static Action PendingDismiss;
        public static T CreateFlowCoordinator<T>() where T : FlowCoordinator, new() => new GameObject("Menu").AddComponent<T>();
        public static T CreateViewController<T>() where T : ViewController, new() => new GameObject("View").AddComponent<T>();
        public static void PresentFlowCoordinator(FlowCoordinator parent, FlowCoordinator flow)
        {
            Presented = flow;
            foreach (var chart in Resources.FindObjectsOfTypeAll<StandardLevelDetailViewController>()) chart.gameObject.SetActive(false);
            flow.ActivateFlow();
        }
        public static void DismissFlowCoordinator(FlowCoordinator parent, FlowCoordinator flow, Action finished)
        {
            if (PendingDismiss != null) throw new InvalidOperationException("Concurrent dismissals");
            PendingDismiss = () =>
            {
                PendingDismiss = null; Presented = null;
                foreach (var chart in Resources.FindObjectsOfTypeAll<StandardLevelDetailViewController>()) chart.gameObject.SetActive(true);
                finished();
            };
        }
    }
}
