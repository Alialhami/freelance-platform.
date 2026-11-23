Freelance Platform
A C# Windows Forms application built for the NCC Education Analysis, Design and Implementation assignment (1013-1).

How to Run
Requirement: You must have the .NET 10 Desktop Runtime installed on your Windows computer.

Double-click Run.cmd. (Alternatively, open App/FreelancePlatform.exe).

To view the source code, open FreelancePlatform.csproj in Visual Studio.

Note: No default accounts are provided. You must create a new Client and Freelancer account to test the system.

How to Test the Workflow
Register a Client: Click "Create an account", select Client, and fill in the contact details.

Register a Freelancer: Create a second account with a different username and select Freelancer.

Freelancer Profile: Sign in as the Freelancer, go to "My profile", and add skills, interests, and portfolio links.

Post a Project: Sign in as the Client, go to "My projects", and post a new project with a budget, timeline, and required skills.

Submit a Proposal: Sign in as the Freelancer, go to "Available projects", and submit a bid.

Accept Proposal: Sign in as the Client, select the project, and accept the Freelancer's proposal.

Complete & Review: The Client confirms completion, then leaves a star rating and review. The Freelancer can view this in "Completed projects".

Key Business Rules (Marking Scheme Requirements)
Passwords: Must be 8–12 characters, containing at least ONE uppercase and ONE lowercase letter. Digits are optional, but symbols are rejected.

Usernames: Must be unique (case-insensitive) and 3–30 characters long.

Data Storage: Records are saved locally in JSON format at %LOCALAPPDATA%\NCCFreelancePlatform\platform.json. A backup (platform.json.bak) is automatically created.

Build and Test
To rebuild the app, double-click Build.cmd.

To run the automated verification tests, double-click Verify.cmd. This runs isolated checks (including password boundaries and workflow permissions) without touching your real saved accounts.


Thank you for raeding.