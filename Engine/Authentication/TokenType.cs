namespace OpenTap.Authentication
{
    /// <summary> Token Type </summary>
    public enum TokenType
    {
        /// <summary> An access token. </summary>
        AccessToken,
        /// <summary> A refresh token. </summary>
        RefreshToken,
        /// <summary> A identity token. </summary>
        IdentityToken
    }
}