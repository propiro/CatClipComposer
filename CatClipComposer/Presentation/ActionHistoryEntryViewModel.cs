namespace CatClipComposer.Presentation;

public sealed record ActionHistoryEntryViewModel(
    DateTime OccurredUtc,
    string Description)
{
    public string OccurredText => OccurredUtc.ToLocalTime().ToString("g");
}
