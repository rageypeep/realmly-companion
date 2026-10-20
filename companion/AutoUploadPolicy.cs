namespace RealmlyCompanion;

internal enum AutoUploadState { Off, WaitingForFile, WaitingForSave, Ready, Uploading, UpToDate, RetryLater, SignInRequired, FileNeedsAttention }

// Pure scheduling policy. No file access, network access, credentials or UI.
internal sealed class AutoUploadPolicy
{
    private string? _target, _stamp, _handled;
    private DateTimeOffset _stableSince, _nextAttempt;
    private int _failures;
    private bool _authBlocked;
    public AutoUploadState State { get; private set; } = AutoUploadState.Off;

    public void Reset()
    {
        _target = _stamp = _handled = null;
        _stableSince = _nextAttempt = default;
        _failures = 0; _authBlocked = false; State = AutoUploadState.Off;
    }

    public bool Observe(string target, string? stamp, DateTimeOffset now)
    {
        if (_target != target) { Reset(); _target = target; }
        if (_authBlocked) { State = AutoUploadState.SignInRequired; return false; }
        if (stamp is null) { _stamp = null; State = AutoUploadState.WaitingForFile; return false; }
        if (_stamp != stamp) { _stamp = stamp; _handled = null; _stableSince = now; }
        if (_handled == stamp) return false;
        if (now < _nextAttempt) { State = AutoUploadState.RetryLater; return false; }
        if (now - _stableSince < TimeSpan.FromSeconds(10)) { State = AutoUploadState.WaitingForSave; return false; }
        State = AutoUploadState.Ready;
        return true;
    }

    public void Uploading() => State = AutoUploadState.Uploading;
    public void Complete() { _handled = _stamp; _failures = 0; _nextAttempt = default; State = AutoUploadState.UpToDate; }

    public void Fail(Exception error, DateTimeOffset now)
    {
        if (error is RealmlyApiException { StatusCode: 401 or 403 })
        { _authBlocked = true; State = AutoUploadState.SignInRequired; return; }
        if (error is InvalidOperationException && error is not RealmlyApiException ||
            error is RealmlyApiException { StatusCode: >= 400 and < 500 and not (408 or 429) } ||
            error is ArgumentException or UnauthorizedAccessException)
        { _handled = _stamp; State = AutoUploadState.FileNeedsAttention; return; }
        _failures = Math.Min(_failures + 1, 6);
        _nextAttempt = now.AddSeconds(Math.Min(900, 30 * Math.Pow(2, _failures - 1)));
        State = AutoUploadState.RetryLater;
    }
}
