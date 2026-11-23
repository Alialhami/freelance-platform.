namespace FreelancePlatform;

public static class Ui
{
    public static readonly Color Ink = Color.FromArgb(24, 42, 62);
    public static void Configure(Form form, string title, int width = 1040, int height = 780)
    {
        form.Text = title; form.Size = new(width, height); form.MinimumSize = new(width, height);
        form.StartPosition = FormStartPosition.CenterScreen; form.Font = new Font("Segoe UI", 10);
        form.BackColor = Color.White; form.AutoScaleMode = AutoScaleMode.Dpi;
    }
    public static Button Button(string text, Action action)
    {
        var button = new Button { Text = text, AutoSize = true, Height = 36, Padding = new Padding(9, 3, 9, 3), Margin = new Padding(4) };
        button.Click += (_, _) => Safe(action); return button;
    }
    public static void Safe(Action action)
    {
        try { action(); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException or UnauthorizedAccessException or System.Security.SecurityException)
        { MessageBox.Show(ex.Message, "Please check", MessageBoxButtons.OK, MessageBoxIcon.Information); }
    }
    public static Label Label(string text) => new() { Text = text, AutoSize = true, MaximumSize = new Size(900, 0), Margin = new Padding(6) };
    public static TextBox ReadOnlyText() => new() { Multiline = true, ReadOnly = true, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Vertical, BackColor = Color.White, Font = new Font("Segoe UI", 10), BorderStyle = BorderStyle.FixedSingle };
    public static DataGridView Grid() => new()
    {
        Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        MultiSelect = false, RowHeadersVisible = false, BackgroundColor = Color.White, BorderStyle = BorderStyle.FixedSingle,
        AutoGenerateColumns = true, RowTemplate = { Height = 30 }, AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells
    };
    public static Guid Selected(DataGridView grid)
    {
        if (grid.CurrentRow is null) throw new ArgumentException("Select a row first.");
        return grid.CurrentRow.Cells["Id"].Value is Guid id ? id : throw new ArgumentException("Select a saved row first.");
    }
    public static void Bind(DataGridView grid, object source)
    { grid.DataSource = source; if (grid.Columns.Contains("Id")) grid.Columns["Id"]!.Visible = false; }
    public static string ProfileText(Freelancer f, PlatformData d)
    {
        var reviews = d.Reviews.Where(r => r.FreelancerId == f.Id).ToList();
        var average = reviews.Count == 0 ? "No reviews yet" : $"{reviews.Average(r => r.Rating):0.0}/5 ({reviews.Count} reviews)";
        return $"{f.Profile.Name} (@{f.Username})\r\nSkills: {f.Profile.Skills}\r\nInterests: {f.Profile.Interests}\r\nExpertise: {f.Profile.Expertise}\r\n\r\nPortfolio and past work\r\n"
            + (f.Profile.Portfolio.Count == 0 ? "No portfolio items yet." : string.Join("\r\n\r\n", f.Profile.Portfolio.Select(p => $"{p.Title}\r\n{p.Description}\r\n{p.Url}")))
            + $"\r\n\r\nCompleted platform projects: {d.Projects.Count(p => p.FreelancerId == f.Id && p.Status == ProjectStatus.Completed)}\r\nRating: {average}\r\n"
            + string.Join("\r\n", reviews.Select(r => $"{r.Rating}/5 - {r.Comment}"));
    }
}

// A reusable editor keeps field layout out of the domain and service classes.
public class EntryForm : Form
{
    private readonly TableLayoutPanel fields = new() { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(16) };
    private readonly Dictionary<string, Control> controls = [];
    public EntryForm(string title)
    {
        Ui.Configure(this, title, 720, 700); AutoScroll = true;
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 175)); fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Controls.Add(fields);
    }
    public TextBox TextField(string key, string label, string value = "", bool multiline = false, bool password = false)
    {
        var control = new TextBox { Text = value, Width = 430, Multiline = multiline, Height = multiline ? 85 : 30, UseSystemPasswordChar = password,
            ScrollBars = multiline ? ScrollBars.Vertical : ScrollBars.None, AccessibleName = label, MaxLength = multiline ? 4000 : 500 };
        Add(key, label, control); return control;
    }
    public void Choice(string key, string label, string[] values)
    { var c = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 430 }; c.Items.AddRange(values); c.SelectedIndex = 0; Add(key, label, c); }
    public void Number(string key, string label, decimal minimum, decimal maximum, decimal value, int decimals = 0)
    { Add(key, label, new NumericUpDown { Minimum = minimum, Maximum = maximum, Value = Math.Clamp(value, minimum, maximum), DecimalPlaces = decimals, Width = 220, ThousandsSeparator = true }); }
    public void Date(string key, string label, DateTime value)
    { Add(key, label, new DateTimePicker { Format = DateTimePickerFormat.Short, Value = value, Width = 220 }); }
    public void Note(string text) { var label = Ui.Label(text); label.MaximumSize = new Size(610, 0); fields.Controls.Add(label, 0, fields.RowCount++); fields.SetColumnSpan(label, 2); }
    private void Add(string key, string label, Control control)
    {
        controls.Add(key, control); int row = fields.RowCount++;
        fields.Controls.Add(Ui.Label(label), 0, row); fields.Controls.Add(control, 1, row); control.Margin = new Padding(4, 6, 4, 6);
    }
    public string Value(string key) => controls[key].Text;
    public decimal Amount(string key) => ((NumericUpDown)controls[key]).Value;
    public DateTime DateValue(string key) => ((DateTimePicker)controls[key]).Value.Date;
    public void Finish(Action save)
    {
        var panel = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        panel.Controls.Add(Ui.Button("Save", () => { save(); DialogResult = DialogResult.OK; Close(); }));
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true, Height = 36 };
        panel.Controls.Add(cancel); CancelButton = cancel;
        fields.Controls.Add(panel, 1, fields.RowCount++);
    }
}

public sealed class RegistrationForm : EntryForm
{
    // AccountService owns validation and account creation; EntryForm supplies field layout.
    public RegistrationForm(AccountService accounts) : base("Create an account")
    {
        Choice("role", "Account type", ["Client", "Freelancer"]);
        TextField("username", "Username"); TextField("password", "Password", password: true);
        TextField("confirm", "Confirm password", password: true); TextField("name", "Full name");
        TextField("email", "Email"); TextField("contact", "Contact details");
        Note("Username: 3-30 letters, numbers or underscores. Password: 8-12 letters/numbers with uppercase and lowercase letters; a number is optional. Contact details are required for clients. Freelancers complete skills and portfolio after signing in.");
        Finish(() => { accounts.Register(Value("username"), Value("password"), Value("confirm"), Value("role"), Value("name"), Value("email"), Value("contact")); });
    }
    public DialogResult ShowRegistration(IWin32Window owner) => ShowDialog(owner);
}

public sealed class LoginForm : Form
{
    private readonly AccountService accounts;
    private readonly IPlatformRepository repository;
    public LoginForm(IPlatformRepository repository)
    {
        this.repository = repository; accounts = new(repository, new(), new());
        Ui.Configure(this, "Freelance Platform", 760, 540);
        var layout = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Padding = new Padding(42), WrapContents = false };
        layout.Controls.Add(new Label { Text = "Freelance Platform", Font = new Font("Segoe UI", 23, FontStyle.Bold), AutoSize = true, ForeColor = Ui.Ink });
        layout.Controls.Add(Ui.Label("Find skilled people. Turn a project into completed work."));
        layout.Controls.Add(Ui.Label("Username")); var username = new TextBox { Width = 420, AccessibleName = "Username" }; layout.Controls.Add(username);
        layout.Controls.Add(Ui.Label("Password")); var password = new TextBox { Width = 420, UseSystemPasswordChar = true, AccessibleName = "Password" }; layout.Controls.Add(password);
        var show = new CheckBox { Text = "Show password", AutoSize = true }; show.CheckedChanged += (_, _) => password.UseSystemPasswordChar = !show.Checked; layout.Controls.Add(show);
        var buttons = new FlowLayoutPanel { AutoSize = true };
        var signIn = Ui.Button("Sign in", () =>
        {
            var user = accounts.Login(username.Text, password.Text); password.Clear();
            Hide(); try { using var dashboard = new DashboardForm(new PlatformService(this.repository, user.Id)); dashboard.ShowDialog(this); }
            finally { Show(); password.Focus(); }
        });
        buttons.Controls.Add(signIn); buttons.Controls.Add(Ui.Button("Create account", () => { using var registration = new RegistrationForm(accounts); if (registration.ShowRegistration(this) == DialogResult.OK) MessageBox.Show("Account created. You can now sign in.", "Welcome"); }));
        layout.Controls.Add(buttons); AcceptButton = signIn;
        layout.Controls.Add(Ui.Label("New here? Create a Client or Freelancer account.\nYour work is saved automatically on this computer.")); Controls.Add(layout);
    }
}

public sealed class DashboardForm : Form
{
    private readonly PlatformService service;
    private readonly bool isClient;
    private readonly TabControl tabs = new() { Dock = DockStyle.Fill };
    private readonly DataGridView projects = Ui.Grid(), proposals = Ui.Grid(), people = Ui.Grid(), available = Ui.Grid(), completed = Ui.Grid(), bids = Ui.Grid();
    private readonly TextBox projectDetails = Ui.ReadOnlyText(), personDetails = Ui.ReadOnlyText(), availableDetails = Ui.ReadOnlyText(), profile = Ui.ReadOnlyText(), completedDetails = Ui.ReadOnlyText();
    private readonly CheckBox matched = new() { Text = "Match my skills and interests", Checked = true, AutoSize = true };
    private readonly TextBox skillSearch = new() { Width = 290, PlaceholderText = "Skills, separated by commas" };
    public DashboardForm(PlatformService service)
    {
        this.service = service; isClient = service.CurrentUser is Client;
        Ui.Configure(this, "Freelance Platform | " + service.CurrentUser.DashboardTitle);
        var shell = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(16) };
        shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 54)); shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        var top = new FlowLayoutPanel { Dock = DockStyle.Fill };
        top.Controls.Add(new Label { Text = service.CurrentUser.DashboardTitle + "  |  @" + service.CurrentUser.Username, AutoSize = true, Font = new Font("Segoe UI", 17, FontStyle.Bold), ForeColor = Ui.Ink, Margin = new Padding(0, 8, 20, 0) });
        top.Controls.Add(Ui.Button("Refresh", RefreshData)); top.Controls.Add(Ui.Button("Sign out", Close));
        shell.Controls.Add(top, 0, 0); shell.Controls.Add(tabs, 0, 1);
        shell.Controls.Add(Ui.Label("Select a row to see details. Changes save automatically. Use Refresh after editing."), 0, 2); Controls.Add(shell);
        BuildProjectTab();
        if (isClient) BuildPeopleTab(); else { BuildAvailableTab(); BuildCompletedTab(); BuildBidsTab(); }
        BuildProfileTab(); RefreshData();
    }
    private TabPage Page(string title) { var p = new TabPage(title) { Padding = new Padding(10) }; tabs.TabPages.Add(p); return p; }
    private TableLayoutPanel BuildLayout(TabPage page, params float[] heights)
    {
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = heights.Length };
        foreach (var h in heights) layout.RowStyles.Add(h < 0 ? new RowStyle(SizeType.Absolute, -h) : new RowStyle(SizeType.Percent, h));
        page.Controls.Add(layout); return layout;
    }
    private void BuildProjectTab()
    {
        var layout = BuildLayout(Page(isClient ? "My projects" : "Ongoing projects"), -46, 35, 30, 35);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill };
        if (isClient)
        {
            actions.Controls.Add(Ui.Button("Post project", PostProject));
            actions.Controls.Add(Ui.Button("Accept selected proposal", () =>
            {
                var id = Ui.Selected(proposals);
                if (MessageBox.Show("Assign this freelancer and reject the other pending proposals?", "Select freelancer", MessageBoxButtons.YesNo) == DialogResult.Yes)
                { service.AcceptProposal(id); RefreshData(); }
            }));
            actions.Controls.Add(Ui.Button("Confirm completion", () =>
            { var id = Ui.Selected(projects); if (MessageBox.Show("Confirm that the project has been completed?", "Complete project", MessageBoxButtons.YesNo) == DialogResult.Yes) { service.CompleteProject(id); RefreshData(); } }));
            actions.Controls.Add(Ui.Button("Leave review", LeaveReview));
        }
        else actions.Controls.Add(Ui.Label("Projects assigned to you, ordered by deadline. The client confirms completion."));
        layout.Controls.Add(actions, 0, 0); layout.Controls.Add(projects, 0, 1); layout.Controls.Add(projectDetails, 0, 2);
        if (isClient) layout.Controls.Add(proposals, 0, 3); else layout.Controls.Add(Ui.Label("Deadline status is shown above. Completed work appears in Completed projects."), 0, 3);
        projects.SelectionChanged += (_, _) => ShowProject();
        proposals.SelectionChanged += (_, _) =>
        {
            if (proposals.CurrentRow is null) return;
            var d = service.Snapshot(); var prop = d.Proposals.Single(p => p.Id == Ui.Selected(proposals));
            var f = d.Users.OfType<Freelancer>().Single(u => u.Id == prop.FreelancerId);
            projectDetails.Text = $"Proposal: {prop.Message}\r\nBid: {prop.Amount:N2} | Delivery: {prop.Days} days | {prop.Status}\r\n\r\n" + Ui.ProfileText(f, d);
        };
    }
    private string ProjectText(Project p, PlatformData d)
    {
        var client = d.Users.OfType<Client>().Single(c => c.Id == p.ClientId);
        var freelancer = d.Users.OfType<Freelancer>().SingleOrDefault(f => f.Id == p.FreelancerId);
        return $"{p.Title}\r\n{p.Description}\r\nRequired skills: {p.Skills}\r\nBudget: {p.Budget:N2} | Timeline: {p.StartDate:d} to {p.Deadline:d}\r\nClient: {client.Profile.Name} | Status: {p.Status} | Freelancer: {freelancer?.Profile.Name ?? "Not assigned"}\r\n"
            + string.Join("\r\n", d.Reviews.Where(r => r.ProjectId == p.Id).Select(r => $"Review: {r.Rating}/5 - {r.Comment}"));
    }
    private void ShowProject()
    {
        if (projects.CurrentRow is null) { projectDetails.Text = "No projects yet."; Ui.Bind(proposals, new List<object>()); return; }
        var d = service.Snapshot(); var p = d.Projects.Single(p => p.Id == Ui.Selected(projects));
        if (isClient)
        {
            Ui.Bind(proposals, d.Proposals.Where(x => x.ProjectId == p.Id).Select(x => new { x.Id, Freelancer = d.Users.Single(u => u.Id == x.FreelancerId).Username, Bid = x.Amount, DeliveryDays = x.Days, Status = x.Status.ToString() }).ToList());
        }
        projectDetails.Text = ProjectText(p, d) + (isClient ? "\r\nSelect a proposal below to inspect the freelancer and past work." : "");
    }
    private void BuildPeopleTab()
    {
        var layout = BuildLayout(Page("Find freelancers"), -46, 40, 60);
        var search = new FlowLayoutPanel { Dock = DockStyle.Fill }; search.Controls.Add(skillSearch); search.Controls.Add(Ui.Button("Search", RefreshPeople)); search.Controls.Add(Ui.Label("Blank search shows everyone."));
        layout.Controls.Add(search, 0, 0); layout.Controls.Add(people, 0, 1); layout.Controls.Add(personDetails, 0, 2);
        people.SelectionChanged += (_, _) => { if (people.CurrentRow is null) { personDetails.Text = "No matching freelancers."; return; } var d = service.Snapshot(); personDetails.Text = Ui.ProfileText(d.Users.OfType<Freelancer>().Single(f => f.Id == Ui.Selected(people)), d); };
    }
    private void RefreshPeople()
    { Ui.Bind(people, service.FindFreelancers(skillSearch.Text).Select(f => new { f.Id, Name = f.Profile.Name, f.Username, f.Profile.Skills, f.Profile.Expertise }).ToList()); }
    private void BuildAvailableTab()
    {
        var layout = BuildLayout(Page("Available projects"), -46, 45, 55);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill }; actions.Controls.Add(matched); matched.CheckedChanged += (_, _) => RefreshAvailable();
        actions.Controls.Add(Ui.Button("Submit proposal", SubmitProposal)); layout.Controls.Add(actions, 0, 0); layout.Controls.Add(available, 0, 1); layout.Controls.Add(availableDetails, 0, 2);
        available.SelectionChanged += (_, _) => { if (available.CurrentRow is null) { availableDetails.Text = "No matches. Complete your profile or turn off the matching filter to browse all open projects."; return; } var d = service.Snapshot(); availableDetails.Text = ProjectText(d.Projects.Single(p => p.Id == Ui.Selected(available)), d); };
    }
    private void RefreshAvailable() => Ui.Bind(available, service.AvailableProjects(matched.Checked).Select(p => new { p.Id, p.Title, p.Skills, p.Budget, Start = p.StartDate.ToShortDateString(), Deadline = p.Deadline.ToShortDateString() }).ToList());
    private void BuildCompletedTab()
    {
        var layout = BuildLayout(Page("Completed projects"), 45, 55); layout.Controls.Add(completed, 0, 0); layout.Controls.Add(completedDetails, 0, 1);
        completed.SelectionChanged += (_, _) => { if (completed.CurrentRow is null) { completedDetails.Text = "No completed projects yet."; return; } var d = service.Snapshot(); completedDetails.Text = ProjectText(d.Projects.Single(p => p.Id == Ui.Selected(completed)), d); };
    }
    private void BuildBidsTab()
    { var page = Page("My proposals"); page.Controls.Add(bids); }
    private void BuildProfileTab()
    {
        var layout = BuildLayout(Page("My profile"), -46, 100); var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill };
        buttons.Controls.Add(Ui.Button("Edit profile", EditProfile));
        if (!isClient) buttons.Controls.Add(Ui.Button("Add portfolio item", AddPortfolio));
        layout.Controls.Add(buttons, 0, 0); layout.Controls.Add(profile, 0, 1);
    }
    public void RefreshData()
    {
        var d = service.Snapshot(); var user = service.CurrentUser;
        var own = d.Projects.Where(p => isClient ? p.ClientId == user.Id : p.FreelancerId == user.Id && p.Status == ProjectStatus.InProgress).OrderBy(p => p.Deadline).ToList();
        Ui.Bind(projects, own.Select(p => new { p.Id, p.Title, p.Budget, Status = p.Status.ToString(), Deadline = p.Deadline.ToShortDateString(), Due = p.Status == ProjectStatus.Completed ? "Completed" : p.Deadline < DateTime.Today ? "Overdue" : $"{(p.Deadline - DateTime.Today).Days} days" }).ToList()); ShowProject();
        if (isClient)
        { RefreshPeople(); var c = (Client)user; profile.Text = $"{c.Profile.Name}\r\nUsername: {c.Username}\r\nEmail: {c.Profile.Email}\r\nContact: {c.Profile.Contact}"; }
        else
        {
            RefreshAvailable(); Ui.Bind(completed, d.Projects.Where(p => p.FreelancerId == user.Id && p.Status == ProjectStatus.Completed).Select(p => new { p.Id, p.Title, p.Budget, Deadline = p.Deadline.ToShortDateString() }).ToList());
            Ui.Bind(bids, d.Proposals.Where(p => p.FreelancerId == user.Id).Select(p => new { p.Id, Project = d.Projects.Single(x => x.Id == p.ProjectId).Title, p.Amount, p.Days, Status = p.Status.ToString(), p.Message }).ToList());
            profile.Text = Ui.ProfileText((Freelancer)user, d);
        }
    }
    private void PostProject()
    {
        using var f = new EntryForm("Post a project"); f.TextField("title", "Title"); f.TextField("description", "Description", multiline: true); f.TextField("skills", "Required skills");
        f.Number("budget", "Budget", 0.01m, 10000000m, 100m, 2); f.Date("start", "Start date", DateTime.Today); f.Date("deadline", "Deadline", DateTime.Today.AddDays(14));
        f.Note("Enter skills separated by commas. Budget and proposal amounts use the same currency agreed between client and freelancer. The posted deadline remains the agreed deadline after selection.");
        f.Finish(() => service.PostProject(f.Value("title"), f.Value("description"), f.Value("skills"), f.Amount("budget"), f.DateValue("start"), f.DateValue("deadline")));
        if (f.ShowDialog(this) == DialogResult.OK) RefreshData();
    }
    private void SubmitProposal()
    {
        var id = Ui.Selected(available); using var f = new EntryForm("Submit a proposal"); f.TextField("message", "Your proposal", multiline: true);
        f.Number("amount", "Bid amount", 0.01m, 10000000m, 100m, 2); f.Number("days", "Delivery days", 1, 3650, 7);
        f.Note("Explain your approach and relevant experience. Your delivery estimate does not change the posted deadline; agree any change with the client before acceptance.");
        f.Finish(() => service.SubmitProposal(id, f.Value("message"), f.Amount("amount"), (int)f.Amount("days"))); if (f.ShowDialog(this) == DialogResult.OK) RefreshData();
    }
    private void LeaveReview()
    {
        var id = Ui.Selected(projects); using var f = new EntryForm("Review completed work"); f.Number("rating", "Rating from 1 to 5", 1, 5, 5); f.TextField("comment", "Review", multiline: true);
        f.Finish(() => service.LeaveReview(id, (int)f.Amount("rating"), f.Value("comment"))); if (f.ShowDialog(this) == DialogResult.OK) RefreshData();
    }
    private void EditProfile()
    {
        var user = service.CurrentUser; using var f = new EntryForm("Edit your profile");
        if (user is Client c)
        {
            f.TextField("name", "Full name", c.Profile.Name); f.TextField("email", "Email", c.Profile.Email); f.TextField("contact", "Contact details", c.Profile.Contact);
            f.Finish(() => service.UpdateClientProfile(new(f.Value("name"), f.Value("email"), f.Value("contact"))));
        }
        else
        {
            var p = ((Freelancer)user).Profile; f.TextField("name", "Full name", p.Name); f.TextField("email", "Email", p.Email); f.TextField("skills", "Skills", p.Skills); f.TextField("interests", "Interests", p.Interests); f.TextField("expertise", "Expertise", p.Expertise, multiline: true);
            f.Note("Separate skills and interests with commas. Matching compares complete terms, ignoring case. For example, C# matches c#.");
            f.Finish(() => service.UpdateFreelancerProfile(new(f.Value("name"), f.Value("email"), f.Value("skills"), f.Value("interests"), f.Value("expertise"), p.Portfolio)));
        }
        if (f.ShowDialog(this) == DialogResult.OK) RefreshData();
    }
    private void AddPortfolio()
    {
        var p = ((Freelancer)service.CurrentUser).Profile; using var f = new EntryForm("Add portfolio and past work");
        f.TextField("title", "Work title"); f.TextField("description", "Past work", multiline: true); f.TextField("url", "Portfolio link");
        f.Finish(() => { var items = p.Portfolio.ToList(); items.Add(new(f.Value("title"), f.Value("description"), f.Value("url"))); service.UpdateFreelancerProfile(new(p.Name, p.Email, p.Skills, p.Interests, p.Expertise, items)); });
        if (f.ShowDialog(this) == DialogResult.OK) RefreshData();
    }
}
