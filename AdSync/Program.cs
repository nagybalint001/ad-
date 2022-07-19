using Azure.Identity;
using Microsoft.Graph;
using System.DirectoryServices.AccountManagement;

var domainName = Environment.GetEnvironmentVariable("USERDNSDOMAIN");

var tenantId = "<tenant-id>";
var clientId = "<client-id>";
var clientSecret = "<client-secret>";
var groupId = "<aad-group-id>";
var defaultPassword = "<default-password>";

var graphClient = GetGraphClient();
var aadUsers = (await GetAllUserFromAAD()).ConvertAll(x => new
{
    x.DisplayName,
    x.GivenName,
    x.Surname,
    Username = x.UserPrincipalName.Split("@")[0],
});

using var domainContext = new PrincipalContext(ContextType.Domain, domainName);
using var usersGroup = GroupPrincipal.FindByIdentity(domainContext, IdentityType.Name, "Domain Users");

using var p = new UserPrincipal(domainContext);
using var searcher = new PrincipalSearcher(p);
using var currentUsers = searcher.FindAll();

var newUsers = aadUsers.Where(x => !currentUsers.Any(y => y.SamAccountName == x.Username));
foreach (var item in newUsers)
{
    CreateUser(item.Username, item.GivenName, item.Surname, item.DisplayName, defaultPassword);
}
Console.WriteLine("Done");


void CreateUser(string username, string firstName, string lastName, string displayName, string password)
{
    using var user = new UserPrincipal(domainContext)
    {
        UserPrincipalName = $"{username}@{domainName.ToLower()}",
        SamAccountName = username,
        Name = displayName,
        DisplayName = displayName,
        GivenName = firstName,
        Surname = lastName,
        Enabled = true,
        PasswordNeverExpires = true,
    };
    user.SetPassword(password);
    user.Save();

    usersGroup.Members.Add(user);
}

GraphServiceClient GetGraphClient()
{
    var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
    return new GraphServiceClient(credential);
}

async Task<List<User>> GetAllUserFromAAD()
{
    var request = graphClient.Groups[groupId].Members.Request();
    var users = new List<User>();

    while (request != null)
    {
        var res = await request.GetAsync();

        users.AddRange(res.CurrentPage.OfType<User>());

        request = res.NextPageRequest;
    }

    return users;
}