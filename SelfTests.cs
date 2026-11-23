namespace FreelancePlatform;

// No test framework or third-party package is needed. Test data never touches live accounts.
public static class SelfTests
{
    private static int passed, failed;
    private static readonly List<string> results = [];
    private static void Check(string name, Action action)
    {
        try { action(); passed++; results.Add("PASS " + name); }
        catch (Exception ex) { failed++; results.Add("FAIL " + name + ": " + ex.Message); }
    }
    private static void Assert(bool condition, string message = "Unexpected result") { if (!condition) throw new Exception(message); }
    private static void Reject(Action action)
    {
        try { action(); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException or UnauthorizedAccessException) { return; }
        throw new Exception("Invalid action was accepted");
    }
    public static int Run(string logPath)
    {
        passed = failed = 0; results.Clear();
        var directory = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(logPath))!, "freelance-tests-" + Guid.NewGuid()); Directory.CreateDirectory(directory);
        var repo = new JsonPlatformRepository(Path.Combine(directory, "platform.json"));
        var policy = new PasswordPolicy(); var hasher = new PasswordHasher(); var accounts = new AccountService(repo, policy, hasher);
        foreach (var value in new[] { "Abcdefgh", "Abcdefghijkl", "Abcdefg1" }) Check("Valid password " + value.Length + " characters: " + value, () => policy.Validate(value));
        foreach (var value in new[] { "Abcdefg", "Abcdefghijklm", "abcdefgh", "ABCDEFGH", "12345678", "Abcdefg!", "Abc defg", "Abcdefg\n", "", "Abcdefé1" })
            Check("Reject invalid password " + value.Replace("\n", "[newline]"), () => Reject(() => policy.Validate(value)));
        Check("Empty repository", () => Assert(repo.Read().Users.Count == 0));
        var client = accounts.Register("client_one", "Client123", "Client123", "Client", "Amal Ali", "amal@example.com", "amal@example.com");
        var otherClient = accounts.Register("client_two", "Client456", "Client456", "Client", "Omar Saleh", "omar@example.com", "omar@example.com");
        var freelancer = accounts.Register("freelancer_one", "Freelance1", "Freelance1", "Freelancer", "Sara Ahmed", "sara@example.com", "");
        var otherFreelancer = accounts.Register("freelancer_two", "Freelance2", "Freelance2", "Freelancer", "Yusuf Nuri", "yusuf@example.com", "");
        var cs = new PlatformService(repo, client.Id); var os = new PlatformService(repo, otherClient.Id);
        var fs = new PlatformService(repo, freelancer.Id); var ff = new PlatformService(repo, otherFreelancer.Id);
        Check("Both roles persisted with runtime types", () => Assert(repo.Read().Users.Count == 4 && repo.Read().Users[0] is Client && repo.Read().Users[2] is Freelancer));
        Check("Username uniqueness ignores case and trims spaces", () => Reject(() => accounts.Register(" CLIENT_ONE ", "Client123", "Client123", "Client", "A", "a@example.com", "a@example.com")));
        Check("Reject mismatched password confirmation", () => Reject(() => accounts.Register("new_client", "Client123", "Wrong123", "Client", "A", "a@example.com", "a@example.com")));
        Check("Reject malformed email", () => Reject(() => accounts.Register("new_client", "Client123", "Client123", "Client", "A", "invalid", "contact")));
        Check("Reject missing client contact", () => Reject(() => accounts.Register("new_client", "Client123", "Client123", "Client", "A", "a@example.com", " ")));
        Check("Reject invalid role", () => Reject(() => accounts.Register("new_client", "Client123", "Client123", "Admin", "A", "a@example.com", "contact")));
        Check("Reject invalid username", () => Reject(() => accounts.Register("x!", "Client123", "Client123", "Client", "A", "a@example.com", "contact")));
        Check("Login case insensitive", () => Assert(accounts.Login("CLIENT_ONE", "Client123").Id == client.Id));
        Check("Reject wrong password", () => Reject(() => accounts.Login("client_one", "Wrong123")));
        Check("Reject unknown user", () => Reject(() => accounts.Login("unknown", "Client123")));
        Check("Password never stored as plaintext", () => Assert(!File.ReadAllText(Path.Combine(directory, "platform.json")).Contains("Client123")));
        Check("Independent random salts", () => Assert(hasher.Hash("Client123") != hasher.Hash("Client123")));
        Check("Malformed password hashes fail safely", () => Assert(!hasher.Verify("Client123", "invalid")));
        Check("Update client contact profile", () => { cs.UpdateClientProfile(new("Amal Ali", "amal@example.com", "Office 12")); Assert(((Client)cs.CurrentUser).Profile.Contact == "Office 12"); });
        Check("Update skills interests expertise portfolio", () => { fs.UpdateFreelancerProfile(new("Sara Ahmed", "sara@example.com", "C#, UI design", "data", "Desktop application development", [new("Booking tool", "Designed a booking interface.", "https://example.com/portfolio")])); Assert(((Freelancer)fs.CurrentUser).Profile.Portfolio.Count == 1); });
        Check("Reject unsafe portfolio URL", () => Reject(() => new PortfolioItem("Example", "Work", "javascript:alert(1)")));
        Check("Prevent wrong role editing freelancer profile", () => Reject(() => cs.UpdateFreelancerProfile(new("A", "a@example.com", "C#", "", "Work", []))));
        Check("Prevent wrong role editing client profile", () => Reject(() => fs.UpdateClientProfile(new("A", "a@example.com", "Office"))));
        var project = cs.PostProject("Booking desktop tool", "Create a booking tool with input validation.", "c#, UI design", 800, DateTime.Today, DateTime.Today.AddDays(14));
        var dataProject = cs.PostProject("Data review", "Review a small dataset.", "Data", 200, DateTime.Today, DateTime.Today.AddDays(7));
        cs.PostProject("Unrelated project", "Develop a Java tool.", "Java", 500, DateTime.Today, DateTime.Today.AddDays(20));
        Check("Project persists description budget timeline", () => Assert(repo.Read().Projects[0].Budget == 800 && repo.Read().Projects[0].Deadline == DateTime.Today.AddDays(14)));
        Check("Reject zero budget", () => Reject(() => cs.PostProject("X", "Y", "C#", 0, DateTime.Today, DateTime.Today)));
        Check("Reject blank project description", () => Reject(() => cs.PostProject("X", " ", "C#", 100, DateTime.Today, DateTime.Today)));
        Check("Reject reversed timeline", () => Reject(() => cs.PostProject("X", "Y", "C#", 100, DateTime.Today.AddDays(1), DateTime.Today)));
        Check("Reject past start", () => Reject(() => cs.PostProject("X", "Y", "C#", 100, DateTime.Today.AddDays(-1), DateTime.Today)));
        Check("Freelancer cannot post project", () => Reject(() => fs.PostProject("X", "Y", "C#", 100, DateTime.Today, DateTime.Today)));
        Check("Match skills and interests case insensitively", () => Assert(fs.AvailableProjects(true).Count == 2 && fs.AvailableProjects(true)[0].Id == dataProject.Id));
        Check("Browse all open projects", () => Assert(fs.AvailableProjects(false).Count == 3));
        Check("Search matching freelancer skills", () => Assert(cs.FindFreelancers("c#").Single().Id == freelancer.Id));
        Check("Blank skills search shows all freelancers", () => Assert(cs.FindFreelancers("").Count == 2));
        var proposal = fs.SubmitProposal(project.Id, "I will build and test the booking workflow.", 750, 10);
        ff.SubmitProposal(project.Id, "I can deliver a second approach.", 780, 12);
        Check("Proposal persisted", () => Assert(repo.Read().Proposals.Count == 2));
        Check("Reject duplicate bid", () => Reject(() => fs.SubmitProposal(project.Id, "Duplicate", 600, 5)));
        Check("Client cannot bid", () => Reject(() => cs.SubmitProposal(dataProject.Id, "Bid", 100, 2)));
        Check("Reject nonpositive bid", () => Reject(() => fs.SubmitProposal(dataProject.Id, "Bid", 0, 2)));
        Check("Reject zero delivery days", () => Reject(() => fs.SubmitProposal(dataProject.Id, "Bid", 100, 0)));
        Check("Reject review before completion", () => Reject(() => cs.LeaveReview(project.Id, 5, "Early review")));
        Check("Other client cannot accept proposal", () => Reject(() => os.AcceptProposal(proposal.Id)));
        Check("Freelancer cannot accept proposal", () => Reject(() => fs.AcceptProposal(proposal.Id)));
        Check("Accept winner reject competing proposals", () => { cs.AcceptProposal(proposal.Id); var d = repo.Read(); Assert(d.Projects.Single(p => p.Id == project.Id).FreelancerId == freelancer.Id && d.Proposals.Count(p => p.Status == ProposalStatus.Accepted) == 1 && d.Proposals.Count(p => p.Status == ProposalStatus.Rejected) == 1); });
        Check("Assigned project excluded from open browse", () => Assert(fs.AvailableProjects(false).Count == 2));
        Check("Reject second acceptance", () => Reject(() => cs.AcceptProposal(proposal.Id)));
        Check("Reject bid on assigned project", () => Reject(() => ff.SubmitProposal(project.Id, "Late bid", 100, 2)));
        Check("Other client cannot complete", () => Reject(() => os.CompleteProject(project.Id)));
        Check("Freelancer cannot complete", () => Reject(() => fs.CompleteProject(project.Id)));
        Check("Open project cannot be completed", () => Reject(() => cs.CompleteProject(dataProject.Id)));
        Check("Owner confirms completion", () => { cs.CompleteProject(project.Id); Assert(repo.Read().Projects.Single(p => p.Id == project.Id).Status == ProjectStatus.Completed); });
        Check("Reject repeated completion", () => Reject(() => cs.CompleteProject(project.Id)));
        Check("Reject rating outside 1 to 5", () => Reject(() => cs.LeaveReview(project.Id, 6, "Good")));
        Check("Other client cannot review", () => Reject(() => os.LeaveReview(project.Id, 5, "Good")));
        Check("Freelancer cannot review", () => Reject(() => fs.LeaveReview(project.Id, 5, "Good")));
        Check("Owner reviews completed project", () => { cs.LeaveReview(project.Id, 5, "Clear communication and reliable work."); Assert(repo.Read().Reviews.Single().FreelancerId == freelancer.Id); });
        Check("Reject duplicate review", () => Reject(() => cs.LeaveReview(project.Id, 4, "Again")));
        Check("Reload retains accounts projects proposals reviews", () => { var d = new JsonPlatformRepository(Path.Combine(directory, "platform.json")).Read(); Assert(d.Users.Count == 4 && d.Projects.Count == 3 && d.Proposals.Count == 2 && d.Reviews.Count == 1); });
        Check("Previous save backup exists", () => Assert(File.Exists(Path.Combine(directory, "platform.json.bak"))));
        Check("Unreadable JSON is reported without reset", () => { var path = Path.Combine(directory, "broken.json"); File.WriteAllText(path, "broken"); var broken = new JsonPlatformRepository(path); Reject(() => broken.Read()); Assert(File.ReadAllText(path) == "broken"); });
        Check("Null collection is reported", () => { var path = Path.Combine(directory, "null.json"); File.WriteAllText(path, "{\"Users\":null}"); Reject(() => new JsonPlatformRepository(path).Read()); });
        Check("Read returns independent snapshots", () => { var d = repo.Read(); d.Users.Clear(); Assert(repo.Read().Users.Count == 4); });
        Check("Polymorphic dashboard titles", () => Assert(client.DashboardTitle != freelancer.DashboardTitle));
        Check("Login form constructs and renders", () => { using var f = new LoginForm(repo); Render(f, Path.Combine(directory, "login.png")); });
        Check("Registration form constructs and renders", () => { using var f = new RegistrationForm(accounts); Render(f, Path.Combine(directory, "registration.png")); });
        Check("Client dashboard constructs with saved records", () => { using var f = new DashboardForm(cs); Render(f, Path.Combine(directory, "client.png")); Assert(f.Controls.Count > 0); });
        Check("Freelancer dashboard constructs with saved records", () => { using var f = new DashboardForm(fs); Render(f, Path.Combine(directory, "freelancer.png")); Assert(f.Controls.Count > 0); });
        results.Insert(0, $"Freelance Platform verification | {DateTimeOffset.UtcNow:O}\n{passed} passed; {failed} failed\nTemporary isolated data: {directory}\n");
        File.WriteAllLines(Path.GetFullPath(logPath), results);
        return failed == 0 ? 0 : 1;
    }
    private static void Render(Form form, string file)
    {
        form.ShowInTaskbar = false; form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-20000, -20000);
        form.Show(); Application.DoEvents(); form.PerformLayout(); Application.DoEvents();
        using var bitmap = new Bitmap(form.Width, form.Height); form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size)); bitmap.Save(file); form.Hide();
    }
    public static int Evidence(string output)
    {
        Directory.CreateDirectory(output);
        var repo = new JsonPlatformRepository(Path.Combine(output, "demo-platform.json"));
        if (repo.Read().Users.Count != 0) throw new InvalidOperationException("Evidence directory must contain no demo accounts yet.");
        var accounts = new AccountService(repo, new(), new());
        var c = accounts.Register("amal_client", "Client123", "Client123", "Client", "Amal Ali", "amal@example.com", "amal@example.com");
        var f = accounts.Register("sara_developer", "Freelance1", "Freelance1", "Freelancer", "Sara Ahmed", "sara@example.com", "");
        var cs = new PlatformService(repo, c.Id); var fs = new PlatformService(repo, f.Id);
        fs.UpdateFreelancerProfile(new("Sara Ahmed", "sara@example.com", "C#, UI design", "Education", "Desktop software and accessible interfaces", [new("Library catalogue", "Built search and loan screens for a student project.", "https://example.com/sara")]));
        var p = cs.PostProject("Library booking application", "Create a desktop application to manage room bookings, validate dates and show availability.", "C#, UI design", 850, DateTime.Today, DateTime.Today.AddDays(14));
        var proposal = fs.SubmitProposal(p.Id, "I will prototype the booking screens, implement validation and test the main workflows.", 800, 12);
        var done = cs.PostProject("Student attendance tool", "Create an attendance recording interface.", "C#", 450, DateTime.Today, DateTime.Today.AddDays(7));
        var doneBid = fs.SubmitProposal(done.Id, "I will build and verify the attendance workflow.", 420, 6); cs.AcceptProposal(doneBid.Id); cs.CompleteProject(done.Id); cs.LeaveReview(done.Id, 5, "Delivered the agreed features and explained the interface clearly.");
        using (var login = new LoginForm(repo)) Render(login, Path.Combine(output, "01-login.png"));
        using (var registration = new RegistrationForm(accounts)) Render(registration, Path.Combine(output, "02-registration.png"));
        using (var client = new DashboardForm(cs)) Render(client, Path.Combine(output, "03-client.png"));
        using (var freelancer = new DashboardForm(fs))
        {
            Render(freelancer, Path.Combine(output, "04-freelancer.png"));
            var tab = freelancer.Controls.OfType<TableLayoutPanel>().Single().Controls.OfType<TabControl>().Single();
            foreach (int index in new[] { 1, 2, 3, 4 }) { tab.SelectedIndex = index; Render(freelancer, Path.Combine(output, $"05-freelancer-tab-{index}.png")); }
        }
        return 0;
    }
}
