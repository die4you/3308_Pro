using MebKoordinatorluk.Core.Entities;

namespace MebKoordinatorluk.Core.Security;

public static class CurrentUserSession
{
    public static User? CurrentUser { get; set; }

    public static bool IsAuthenticated => CurrentUser != null;

    public static bool IsAdmin => CurrentUser != null && CurrentUser.IsAdmin;

    public static bool TeacherCanEditData { get; set; } = false;

    public static bool IsArchiveReadOnly { get; set; } = false;

    public static bool CanEditData => (IsAdmin || TeacherCanEditData) && !IsArchiveReadOnly;

    public static bool RequestedRelogin { get; set; } = false;

    public static void Logout()
    {
        CurrentUser = null;
    }
}
