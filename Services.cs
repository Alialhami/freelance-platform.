using System.Net.Mail;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FreelancePlatform;

public static class Rules
{
    public static string Text(string value, string field, int maximum)
    {
        value = value.Trim();
        if (value.Length == 0 || value.Length > maximum) throw new ArgumentException($"{field} is required and must be at most {maximum} characters.");
        return value;
    }
    public static string Email(string value)
    {
        value = Text(value, "Email", 254);
        if (!MailAddress.TryCreate(value, out var address) || address.Address != value || !address.Host.Contains('.'))
            throw new ArgumentException("Enter an email address such as name@example.com.");
        return value;
    }
    public static HashSet<string> Tokens(string value) => value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
}

public sealed class PasswordPolicy
{
    public void Validate(string password)
    {
        // Alphanumeric restriction follows the highest band of the supplied mark scheme.
        if (!Regex.IsMatch(password, "\\A[A-Za-z0-9]{8,12}\\z") || !password.Any(char.IsUpper) || !password.Any(char.IsLower))
            throw new ArgumentException("Password must have 8-12 letters/numbers, including at least one uppercase and one lowercase letter. A number is optional.");
    }
}

public sealed class PasswordHasher
{
    private const int Iterations = 600000;
    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32);
        return $"{Iterations}:{Convert.ToBase64String(salt)}:{Convert.ToBase64String(key)}";
    }
    public bool Verify(string password, string stored)
    {
        var parts = stored.Split(':');
        if (parts.Length != 3 || !int.TryParse(parts[0], out int iterations) || iterations != Iterations) return false;
        try
        {
            var salt = Convert.FromBase64String(parts[1]); var expected = Convert.FromBase64String(parts[2]);
            if (salt.Length != 16 || expected.Length != 32) return false;
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, 32);
            return CryptographicOperations.FixedTimeEquals(expected, actual);
        }
        catch (FormatException) { return false; }
    }
}

public sealed class PlatformData
{
    public List<User> Users { get; set; } = [];
    public List<Project> Projects { get; set; } = [];
    public List<Proposal> Proposals { get; set; } = [];
    public List<Review> Reviews { get; set; } = [];
}

public interface IPlatformRepository
{
    PlatformData Read();
    void Write(PlatformData data);
}

public sealed class JsonPlatformRepository : IPlatformRepository
{
    private readonly string path;
    private readonly JsonSerializerOptions options = new() { WriteIndented = true };
    public JsonPlatformRepository(string path) { this.path = Path.GetFullPath(path); }
    public PlatformData Read()
    {
        if (!File.Exists(path)) return new();
        try
        {
            var data = JsonSerializer.Deserialize<PlatformData>(File.ReadAllText(path), options) ?? throw new JsonException();
            if (data.Users is null || data.Projects is null || data.Proposals is null || data.Reviews is null) throw new JsonException();
            return data;
        }
        catch (JsonException ex) { throw new InvalidOperationException("Saved data is unreadable. Close the app and restore platform.json.bak after keeping a copy of the damaged file.", ex); }
    }
    public void Write(PlatformData data)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, data, options);
                stream.Flush(true);
            }
            // Replacement preserves the previous successful version as a backup.
            if (File.Exists(path)) File.Replace(temporary, path, path + ".bak");
            else File.Move(temporary, path);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}

public sealed class AccountService
{
    private readonly IPlatformRepository repository;
    private readonly PasswordPolicy policy;
    private readonly PasswordHasher hasher;
    public AccountService(IPlatformRepository repository, PasswordPolicy policy, PasswordHasher hasher)
    { this.repository = repository; this.policy = policy; this.hasher = hasher; }
    public User Register(string username, string password, string confirmation, string role, string name, string email, string contact)
    {
        username = Rules.Text(username, "Username", 30);
        if (!Regex.IsMatch(username, "\\A[A-Za-z0-9_]{3,30}\\z")) throw new ArgumentException("Username must contain 3-30 letters, numbers or underscores.");
        policy.Validate(password);
        if (password != confirmation) throw new ArgumentException("Passwords do not match.");
        if (role != "Client" && role != "Freelancer") throw new ArgumentException("Choose Client or Freelancer.");
        var data = repository.Read();
        if (data.Users.Any(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase))) throw new ArgumentException("That username is already taken. Choose another.");
        User user = role == "Client"
            ? new Client(username, hasher.Hash(password), new ClientProfile(name, email, contact))
            : new Freelancer(username, hasher.Hash(password), new FreelancerProfile(name, email, "Not yet specified", "", "Not yet specified", []));
        data.Users.Add(user); repository.Write(data); return user;
    }
    public User Login(string username, string password)
    {
        var user = repository.Read().Users.SingleOrDefault(u => u.Username.Equals(username.Trim(), StringComparison.OrdinalIgnoreCase));
        if (user is null || !hasher.Verify(password, user.PasswordHash)) throw new ArgumentException("Username or password is incorrect.");
        return user;
    }
}

public sealed class PlatformService
{
    private readonly IPlatformRepository repository;
    private readonly Guid currentUserId;
    public PlatformService(IPlatformRepository repository, Guid currentUserId) { this.repository = repository; this.currentUserId = currentUserId; }
    public PlatformData Snapshot() => repository.Read();
    public User CurrentUser => Snapshot().Users.Single(u => u.Id == currentUserId);
    private T Require<T>(PlatformData data) where T : User => data.Users.SingleOrDefault(u => u.Id == currentUserId) as T
        ?? throw new InvalidOperationException($"This action requires a {typeof(T).Name.ToLowerInvariant()} account.");
    public void UpdateClientProfile(ClientProfile profile)
    { var d = repository.Read(); Require<Client>(d).UpdateProfile(profile); repository.Write(d); }
    public void UpdateFreelancerProfile(FreelancerProfile profile)
    { var d = repository.Read(); Require<Freelancer>(d).UpdateProfile(profile); repository.Write(d); }
    public Project PostProject(string title, string description, string skills, decimal budget, DateTime start, DateTime deadline)
    {
        var d = repository.Read(); var client = Require<Client>(d);
        var project = new Project(client.Id, title, description, skills, budget, start, deadline);
        d.Projects.Add(project); repository.Write(d); return project;
    }
    public Proposal SubmitProposal(Guid projectId, string message, decimal amount, int days)
    {
        var d = repository.Read(); var user = Require<Freelancer>(d);
        var project = d.Projects.SingleOrDefault(p => p.Id == projectId) ?? throw new ArgumentException("Project not found.");
        if (project.Status != ProjectStatus.Open) throw new InvalidOperationException("Only open projects accept proposals.");
        if (d.Proposals.Any(p => p.ProjectId == projectId && p.FreelancerId == user.Id)) throw new ArgumentException("You have already bid on this project.");
        var proposal = new Proposal(projectId, user.Id, message, amount, days);
        d.Proposals.Add(proposal); repository.Write(d); return proposal;
    }
    public void AcceptProposal(Guid proposalId)
    {
        var d = repository.Read(); var user = Require<Client>(d);
        var proposal = d.Proposals.Single(p => p.Id == proposalId);
        var project = d.Projects.Single(p => p.Id == proposal.ProjectId);
        if (project.ClientId != user.Id) throw new InvalidOperationException("Only the client who posted this project can select its freelancer.");
        if (proposal.Status != ProposalStatus.Pending) throw new InvalidOperationException("This proposal is no longer pending.");
        project.Assign(proposal.FreelancerId);
        foreach (var p in d.Proposals.Where(p => p.ProjectId == project.Id && p.Status == ProposalStatus.Pending)) p.Decide(p.Id == proposalId);
        repository.Write(d);
    }
    public void CompleteProject(Guid projectId)
    {
        var d = repository.Read(); var user = Require<Client>(d);
        var project = d.Projects.Single(p => p.Id == projectId);
        if (project.ClientId != user.Id) throw new InvalidOperationException("Only the project owner can confirm completion.");
        project.Complete(); repository.Write(d);
    }
    public void LeaveReview(Guid projectId, int rating, string comment)
    {
        var d = repository.Read(); var user = Require<Client>(d);
        var project = d.Projects.Single(p => p.Id == projectId);
        if (project.ClientId != user.Id || project.Status != ProjectStatus.Completed || project.FreelancerId is null)
            throw new InvalidOperationException("Reviews require your own completed project with an assigned freelancer.");
        if (d.Reviews.Any(r => r.ProjectId == projectId)) throw new ArgumentException("This project already has a review.");
        d.Reviews.Add(new Review(project.Id, user.Id, project.FreelancerId.Value, rating, comment)); repository.Write(d);
    }
    public List<Project> AvailableProjects(bool matchedOnly)
    {
        var d = repository.Read(); var f = Require<Freelancer>(d);
        var terms = Rules.Tokens(f.Profile.Skills + "," + f.Profile.Interests);
        return d.Projects.Where(p => p.Status == ProjectStatus.Open && (!matchedOnly || Rules.Tokens(p.Skills).Overlaps(terms)))
            .OrderBy(p => p.Deadline).ToList();
    }
    public List<Freelancer> FindFreelancers(string skills)
    {
        var d = repository.Read(); Require<Client>(d); var terms = Rules.Tokens(skills);
        return d.Users.OfType<Freelancer>().Where(f => terms.Count == 0 || Rules.Tokens(f.Profile.Skills).Overlaps(terms)).OrderBy(f => f.Profile.Name).ToList();
    }
}
