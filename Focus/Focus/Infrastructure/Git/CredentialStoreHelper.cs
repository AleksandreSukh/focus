using LibGit2Sharp;
using Microsoft.Alm.Authentication;

namespace Systems.Sanity.Focus.Infrastructure.Git
{
    public static class CredentialStoreHelper
    {
        public static UsernamePasswordCredentials GetCredentialsFromMicrosoftCredentialStore()
        {
            var secrets = new SecretStore("git");
            var auth = new BasicAuthentication(secrets);
            var credentials = auth.GetCredentials(new TargetUri("https://github.com"));

            var creds = new UsernamePasswordCredentials()
            {
                Username = credentials.Username,
                Password = credentials.Password
            };
            return creds;
        }
    }
}
