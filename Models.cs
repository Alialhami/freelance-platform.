using System.Text.Json.Serialization;

namespace FreelancePlatform;

// Properties expose data for reading; behaviour controls changes to domain objects.
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$role")]
[JsonDerivedType(typeof(Client), "client")]
[JsonDerivedType(typeof(Freelancer), "freelancer")]
public abstract class User
{
    [JsonInclude] public Guid Id { get; private set; } = Guid.NewGuid();
    [JsonInclude] public string Username { get; private set; } = "";
    [JsonInclude] public string PasswordHash { get; private set; } = "";
    protected User() { }
    protected User(string username, string passwordHash) { Username = username; PasswordHash = passwordHash; }
    [JsonIgnore] public abstract string DashboardTitle { get; }
}

public sealed class Client : User
{
    [JsonInclude] public ClientProfile Profile { get; private set; } = new();
    public Client() { }
    public Client(string username, string hash, ClientProfile profile) : base(username, hash) { Profile = profile; }
    public override string DashboardTitle => "Client workspace";
    public void UpdateProfile(ClientProfile profile) => Profile = profile;
}

public sealed class Freelancer : User
{
    [JsonInclude] public FreelancerProfile Profile { get; private set; } = new();
    public Freelancer() { }
    public Freelancer(string username, string hash, FreelancerProfile profile) : base(username, hash) { Profile = profile; }
    public override string DashboardTitle => "Freelancer workspace";
    public void UpdateProfile(FreelancerProfile profile) => Profile = profile;
}

public sealed class ClientProfile
{
    [JsonInclude] public string Name { get; private set; } = "";
    [JsonInclude] public string Email { get; private set; } = "";
    [JsonInclude] public string Contact { get; private set; } = "";
    public ClientProfile() { }
    public ClientProfile(string name, string email, string contact)
    { Name = Rules.Text(name, "Name", 100); Email = Rules.Email(email); Contact = Rules.Text(contact, "Contact details", 200); }
}

public sealed class FreelancerProfile
{
    [JsonInclude] public string Name { get; private set; } = "";
    [JsonInclude] public string Email { get; private set; } = "";
    [JsonInclude] public string Skills { get; private set; } = "";
    [JsonInclude] public string Interests { get; private set; } = "";
    [JsonInclude] public string Expertise { get; private set; } = "";
    [JsonInclude] public List<PortfolioItem> Portfolio { get; private set; } = [];
    public FreelancerProfile() { }
    public FreelancerProfile(string name, string email, string skills, string interests, string expertise, List<PortfolioItem> portfolio)
    {
        Name = Rules.Text(name, "Name", 100); Email = Rules.Email(email);
        Skills = Rules.Text(skills, "Skills", 400); Interests = interests.Trim();
        Expertise = Rules.Text(expertise, "Expertise", 2000); Portfolio = portfolio;
    }
}

public sealed class PortfolioItem
{
    [JsonInclude] public string Title { get; private set; } = "";
    [JsonInclude] public string Description { get; private set; } = "";
    [JsonInclude] public string Url { get; private set; } = "";
    public PortfolioItem() { }
    public PortfolioItem(string title, string description, string url)
    {
        Title = Rules.Text(title, "Portfolio title", 150);
        Description = Rules.Text(description, "Past work description", 2000);
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) || (uri.Scheme != "https" && uri.Scheme != "http"))
            throw new ArgumentException("Portfolio link must be a complete http or https address.");
        Url = uri.AbsoluteUri;
    }
}

public enum ProjectStatus { Open, InProgress, Completed }
public enum ProposalStatus { Pending, Accepted, Rejected }

public sealed class Project
{
    [JsonInclude] public Guid Id { get; private set; } = Guid.NewGuid();
    [JsonInclude] public Guid ClientId { get; private set; }
    [JsonInclude] public Guid? FreelancerId { get; private set; }
    [JsonInclude] public string Title { get; private set; } = "";
    [JsonInclude] public string Description { get; private set; } = "";
    [JsonInclude] public string Skills { get; private set; } = "";
    [JsonInclude] public decimal Budget { get; private set; }
    [JsonInclude] public DateTime StartDate { get; private set; }
    [JsonInclude] public DateTime Deadline { get; private set; }
    [JsonInclude] public ProjectStatus Status { get; private set; }
    public Project() { }
    public Project(Guid clientId, string title, string description, string skills, decimal budget, DateTime start, DateTime deadline)
    {
        Title = Rules.Text(title, "Project title", 150); Description = Rules.Text(description, "Description", 4000);
        Skills = Rules.Text(skills, "Required skills", 400);
        if (budget <= 0 || budget > 10000000) throw new ArgumentException("Budget must be greater than zero and at most 10,000,000.");
        if (start.Date < DateTime.Today || deadline.Date < start.Date) throw new ArgumentException("Start must be today or later; deadline cannot precede start.");
        ClientId = clientId; Budget = budget; StartDate = start.Date; Deadline = deadline.Date;
    }
    public void Assign(Guid freelancerId)
    {
        if (Status != ProjectStatus.Open) throw new InvalidOperationException("This project is no longer open.");
        FreelancerId = freelancerId; Status = ProjectStatus.InProgress;
    }
    public void Complete()
    {
        if (Status != ProjectStatus.InProgress) throw new InvalidOperationException("Only an ongoing project can be completed.");
        Status = ProjectStatus.Completed;
    }
}

public sealed class Proposal
{
    [JsonInclude] public Guid Id { get; private set; } = Guid.NewGuid();
    [JsonInclude] public Guid ProjectId { get; private set; }
    [JsonInclude] public Guid FreelancerId { get; private set; }
    [JsonInclude] public string Message { get; private set; } = "";
    [JsonInclude] public decimal Amount { get; private set; }
    [JsonInclude] public int Days { get; private set; }
    [JsonInclude] public ProposalStatus Status { get; private set; }
    public Proposal() { }
    public Proposal(Guid projectId, Guid freelancerId, string message, decimal amount, int days)
    {
        Message = Rules.Text(message, "Proposal", 4000);
        if (amount <= 0 || amount > 10000000) throw new ArgumentException("Bid must be greater than zero and at most 10,000,000.");
        if (days < 1 || days > 3650) throw new ArgumentException("Delivery must be between 1 and 3650 days.");
        ProjectId = projectId; FreelancerId = freelancerId; Amount = amount; Days = days;
    }
    public void Decide(bool accepted)
    {
        if (Status != ProposalStatus.Pending) throw new InvalidOperationException("This proposal has already been decided.");
        Status = accepted ? ProposalStatus.Accepted : ProposalStatus.Rejected;
    }
}

public sealed class Review
{
    [JsonInclude] public Guid Id { get; private set; } = Guid.NewGuid();
    [JsonInclude] public Guid ProjectId { get; private set; }
    [JsonInclude] public Guid ClientId { get; private set; }
    [JsonInclude] public Guid FreelancerId { get; private set; }
    [JsonInclude] public int Rating { get; private set; }
    [JsonInclude] public string Comment { get; private set; } = "";
    public Review() { }
    public Review(Guid projectId, Guid clientId, Guid freelancerId, int rating, string comment)
    {
        if (rating < 1 || rating > 5) throw new ArgumentException("Rating must be from 1 to 5.");
        Comment = Rules.Text(comment, "Review", 2000);
        ProjectId = projectId; ClientId = clientId; FreelancerId = freelancerId; Rating = rating;
    }
}
