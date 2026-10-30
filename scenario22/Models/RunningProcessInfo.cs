using System.Windows.Media;

namespace Scenario22.Models;

/// <summary>
/// A process shown in the running-process picker.
/// </summary>
public sealed record RunningProcessInfo(
    string Name,
    string Path,
    string Title,
    bool IsProtected,
    ImageSource? Icon);
