namespace Socotra;

public partial class Panel
{
    private CancellationTokenSource? _deleteTokenSource;

    /// <summary>Anything you want to keep with the panel, without making a new panel type for it.</summary>
    public object? UserData { get; set; }

    /// <summary>A token that's canceled when the panel is deleted, for stopping async work that belongs to it.</summary>
    public CancellationToken DeletionToken
    {
        get
        {
            if (IsDeleting || IsDeleted)
            {
                return CancellationToken.None;
            }

            _deleteTokenSource ??= new CancellationTokenSource();
            return _deleteTokenSource.Token;
        }
    }
}
