using Microsoft.EntityFrameworkCore;
using Plandex.Api.Models;
using Plandex.Api.Services;

namespace Plandex.Api.Data;

public static class DbSeeder
{
    public const string DemoEmail = "demo@plandex.dev";
    public const string DemoPassword = "demo1234";

    public const string Demo2Email = "demo2@plandex.dev";
    public const string Demo2Password = "demo1234";

    public static async Task SeedAsync(PlandexDbContext db, IPasswordHasher hasher, CancellationToken ct = default)
    {
        if (await db.Users.AnyAsync(ct)) return;

        var now = DateTime.UtcNow;

        var demo = new User
        {
            Email = DemoEmail,
            Name = "Demo User",
            PasswordHash = hasher.Hash(DemoPassword),
            CreatedAt = now
        };
        var demo2 = new User
        {
            Email = Demo2Email,
            Name = "Demo Collaborator",
            PasswordHash = hasher.Hash(Demo2Password),
            CreatedAt = now
        };
        db.Users.AddRange(demo, demo2);
        await db.SaveChangesAsync(ct);

        await SeedPersonalBoardAsync(db, demo.Id, now, ct);
        await SeedWebsiteBoardAsync(db, demo.Id, now, ct);
        var roadmapBoardId = await SeedRoadmapBoardAsync(db, demo.Id, now, ct);

        // Pre-share the roadmap board with demo2 so the collaboration feature is
        // discoverable the moment you log in as either user.
        db.BoardMembers.Add(new BoardMember
        {
            BoardId = roadmapBoardId,
            UserId = demo2.Id,
            Role = BoardRole.Member,
            AddedAt = now,
        });
        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedPersonalBoardAsync(PlandexDbContext db, int ownerId, DateTime now, CancellationToken ct)
    {
        var board = new Board { Name = "Personal Tasks", OwnerId = ownerId, CreatedAt = now };
        db.Boards.Add(board);
        await db.SaveChangesAsync(ct);
        db.BoardMembers.Add(new BoardMember { BoardId = board.Id, UserId = ownerId, Role = BoardRole.Owner, AddedAt = now });
        await db.SaveChangesAsync(ct);

        var lHome = new Label { BoardId = board.Id, Name = "Home", Color = "#3B82F6" };
        var lShop = new Label { BoardId = board.Id, Name = "Shopping", Color = "#EAB308" };
        var lUrgent = new Label { BoardId = board.Id, Name = "Urgent", Color = "#EF4444" };
        var lHealth = new Label { BoardId = board.Id, Name = "Health", Color = "#10B981" };
        db.Labels.AddRange(lHome, lShop, lUrgent, lHealth);

        var backlog = new BoardList { BoardId = board.Id, Name = "Backlog", Position = 0, CreatedAt = now };
        var week = new BoardList { BoardId = board.Id, Name = "This Week", Position = 1, CreatedAt = now };
        var progress = new BoardList { BoardId = board.Id, Name = "In Progress", Position = 2, CreatedAt = now };
        var done = new BoardList { BoardId = board.Id, Name = "Done", Position = 3, CreatedAt = now };
        db.Lists.AddRange(backlog, week, progress, done);
        await db.SaveChangesAsync(ct);

        var cards = new[]
        {
            new Card { ListId = backlog.Id, Title = "Plan summer vacation", Description = "Research destinations, compare flights, pick dates.", Position = 0, CreatedAt = now },
            new Card { ListId = backlog.Id, Title = "Renew passport", Position = 1, DueDate = now.AddDays(45), CreatedAt = now },
            new Card { ListId = backlog.Id, Title = "Organize garage", Position = 2, CreatedAt = now },

            new Card { ListId = week.Id, Title = "Weekly groceries", Description = "Milk, eggs, bread, tomatoes, pasta, chicken, fruit.", Position = 0, DueDate = now.AddDays(2), CreatedAt = now },
            new Card { ListId = week.Id, Title = "Pay electricity bill", Position = 1, DueDate = now.AddDays(4), CreatedAt = now },
            new Card { ListId = week.Id, Title = "Call mom", Position = 2, CreatedAt = now },
            new Card { ListId = week.Id, Title = "Dentist appointment", Description = "Dr. Novak, 10:00 — bring insurance card.", Position = 3, DueDate = now.AddDays(3), CreatedAt = now },

            new Card { ListId = progress.Id, Title = "Paint living room", Description = "Second coat on the west wall. Needs to dry overnight.", Position = 0, CreatedAt = now },
            new Card { ListId = progress.Id, Title = "Read \"Project Hail Mary\"", Position = 1, CreatedAt = now },

            new Card { ListId = done.Id, Title = "Fix kitchen sink leak", Position = 0, CreatedAt = now.AddDays(-3) },
            new Card { ListId = done.Id, Title = "Book car service", Position = 1, CreatedAt = now.AddDays(-5) },
            new Card { ListId = done.Id, Title = "Submit tax forms", Position = 2, CreatedAt = now.AddDays(-7) },
        };
        db.Cards.AddRange(cards);
        await db.SaveChangesAsync(ct);

        db.CardLabels.AddRange(
            new CardLabel { CardId = cards[3].Id, LabelId = lShop.Id },
            new CardLabel { CardId = cards[4].Id, LabelId = lHome.Id },
            new CardLabel { CardId = cards[4].Id, LabelId = lUrgent.Id },
            new CardLabel { CardId = cards[6].Id, LabelId = lHealth.Id },
            new CardLabel { CardId = cards[7].Id, LabelId = lHome.Id },
            new CardLabel { CardId = cards[1].Id, LabelId = lUrgent.Id }
        );

        var groceryList = new Checklist { CardId = cards[3].Id, Title = "Items" };
        db.Checklists.Add(groceryList);
        await db.SaveChangesAsync(ct);
        db.ChecklistItems.AddRange(
            new ChecklistItem { ChecklistId = groceryList.Id, Text = "Milk (2L)", IsDone = true, Position = 0 },
            new ChecklistItem { ChecklistId = groceryList.Id, Text = "Eggs", IsDone = true, Position = 1 },
            new ChecklistItem { ChecklistId = groceryList.Id, Text = "Whole-wheat bread", IsDone = false, Position = 2 },
            new ChecklistItem { ChecklistId = groceryList.Id, Text = "Chicken breast", IsDone = false, Position = 3 },
            new ChecklistItem { ChecklistId = groceryList.Id, Text = "Olive oil", IsDone = false, Position = 4 }
        );

        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedWebsiteBoardAsync(PlandexDbContext db, int ownerId, DateTime now, CancellationToken ct)
    {
        var board = new Board { Name = "Website Redesign", OwnerId = ownerId, CreatedAt = now };
        db.Boards.Add(board);
        await db.SaveChangesAsync(ct);
        db.BoardMembers.Add(new BoardMember { BoardId = board.Id, UserId = ownerId, Role = BoardRole.Owner, AddedAt = now });
        await db.SaveChangesAsync(ct);

        var lDesign = new Label { BoardId = board.Id, Name = "Design", Color = "#A855F7" };
        var lDev = new Label { BoardId = board.Id, Name = "Dev", Color = "#22C55E" };
        var lBlocked = new Label { BoardId = board.Id, Name = "Blocked", Color = "#EF4444" };
        var lQA = new Label { BoardId = board.Id, Name = "QA", Color = "#F97316" };
        db.Labels.AddRange(lDesign, lDev, lBlocked, lQA);

        var ideas = new BoardList { BoardId = board.Id, Name = "Ideas", Position = 0, CreatedAt = now };
        var todo = new BoardList { BoardId = board.Id, Name = "Todo", Position = 1, CreatedAt = now };
        var progress = new BoardList { BoardId = board.Id, Name = "In Progress", Position = 2, CreatedAt = now };
        var review = new BoardList { BoardId = board.Id, Name = "Review", Position = 3, CreatedAt = now };
        var shipped = new BoardList { BoardId = board.Id, Name = "Shipped", Position = 4, CreatedAt = now };
        db.Lists.AddRange(ideas, todo, progress, review, shipped);
        await db.SaveChangesAsync(ct);

        var cards = new[]
        {
            new Card { ListId = ideas.Id, Title = "Dark mode", Description = "Full theme switcher with system-preference detection.", Position = 0, CreatedAt = now },
            new Card { ListId = ideas.Id, Title = "Newsletter signup", Position = 1, CreatedAt = now },
            new Card { ListId = ideas.Id, Title = "Interactive product demo", Description = "Scroll-driven animation, see [Stripe](https://stripe.com) for reference.", Position = 2, CreatedAt = now },

            new Card { ListId = todo.Id, Title = "New landing hero section", Description = "Replace the stock photo carousel with a single focused hero.", Position = 0, DueDate = now.AddDays(7), CreatedAt = now },
            new Card { ListId = todo.Id, Title = "Redesign pricing page", Position = 1, DueDate = now.AddDays(10), CreatedAt = now },
            new Card { ListId = todo.Id, Title = "Audit accessibility (WCAG 2.2 AA)", Position = 2, CreatedAt = now },
            new Card { ListId = todo.Id, Title = "Migrate blog to MDX", Position = 3, CreatedAt = now },

            new Card { ListId = progress.Id, Title = "Rebuild nav with Tailwind", Description = "Sticky on scroll, mobile drawer, active-section indicator.", Position = 0, DueDate = now.AddDays(3), CreatedAt = now },
            new Card { ListId = progress.Id, Title = "Replace icon set with Lucide", Position = 1, CreatedAt = now },

            new Card { ListId = review.Id, Title = "Homepage copy refresh", Description = "Review from marketing team pending.", Position = 0, CreatedAt = now.AddDays(-2) },
            new Card { ListId = review.Id, Title = "Footer component", Position = 1, CreatedAt = now.AddDays(-1) },

            new Card { ListId = shipped.Id, Title = "Set up analytics events", Position = 0, CreatedAt = now.AddDays(-14) },
            new Card { ListId = shipped.Id, Title = "Fix CLS on product cards", Position = 1, CreatedAt = now.AddDays(-10) },
            new Card { ListId = shipped.Id, Title = "Deploy preview environments", Position = 2, CreatedAt = now.AddDays(-8) },
        };
        db.Cards.AddRange(cards);
        await db.SaveChangesAsync(ct);

        db.CardLabels.AddRange(
            new CardLabel { CardId = cards[0].Id, LabelId = lDesign.Id },
            new CardLabel { CardId = cards[3].Id, LabelId = lDesign.Id },
            new CardLabel { CardId = cards[3].Id, LabelId = lDev.Id },
            new CardLabel { CardId = cards[4].Id, LabelId = lDesign.Id },
            new CardLabel { CardId = cards[5].Id, LabelId = lQA.Id },
            new CardLabel { CardId = cards[7].Id, LabelId = lDev.Id },
            new CardLabel { CardId = cards[8].Id, LabelId = lDev.Id },
            new CardLabel { CardId = cards[9].Id, LabelId = lBlocked.Id },
            new CardLabel { CardId = cards[11].Id, LabelId = lDev.Id },
            new CardLabel { CardId = cards[12].Id, LabelId = lDev.Id },
            new CardLabel { CardId = cards[13].Id, LabelId = lDev.Id }
        );

        var navChecklist = new Checklist { CardId = cards[7].Id, Title = "Acceptance criteria" };
        db.Checklists.Add(navChecklist);
        await db.SaveChangesAsync(ct);
        db.ChecklistItems.AddRange(
            new ChecklistItem { ChecklistId = navChecklist.Id, Text = "Sticky on scroll (>100px)", IsDone = true, Position = 0 },
            new ChecklistItem { ChecklistId = navChecklist.Id, Text = "Mobile drawer animates in 200ms", IsDone = true, Position = 1 },
            new ChecklistItem { ChecklistId = navChecklist.Id, Text = "Active section highlighted", IsDone = false, Position = 2 },
            new ChecklistItem { ChecklistId = navChecklist.Id, Text = "Keyboard navigable", IsDone = false, Position = 3 },
            new ChecklistItem { ChecklistId = navChecklist.Id, Text = "Lighthouse a11y score ≥ 95", IsDone = false, Position = 4 }
        );

        var a11yChecklist = new Checklist { CardId = cards[5].Id, Title = "Pages to audit" };
        db.Checklists.Add(a11yChecklist);
        await db.SaveChangesAsync(ct);
        db.ChecklistItems.AddRange(
            new ChecklistItem { ChecklistId = a11yChecklist.Id, Text = "Homepage", IsDone = false, Position = 0 },
            new ChecklistItem { ChecklistId = a11yChecklist.Id, Text = "Pricing", IsDone = false, Position = 1 },
            new ChecklistItem { ChecklistId = a11yChecklist.Id, Text = "Blog index", IsDone = false, Position = 2 },
            new ChecklistItem { ChecklistId = a11yChecklist.Id, Text = "Signup flow", IsDone = false, Position = 3 }
        );

        await db.SaveChangesAsync(ct);
    }

    private static async Task<int> SeedRoadmapBoardAsync(PlandexDbContext db, int ownerId, DateTime now, CancellationToken ct)
    {
        var board = new Board { Name = "Q2 Roadmap", OwnerId = ownerId, CreatedAt = now };
        db.Boards.Add(board);
        await db.SaveChangesAsync(ct);
        db.BoardMembers.Add(new BoardMember { BoardId = board.Id, UserId = ownerId, Role = BoardRole.Owner, AddedAt = now });
        await db.SaveChangesAsync(ct);

        var lFeature = new Label { BoardId = board.Id, Name = "Feature", Color = "#22C55E" };
        var lBug = new Label { BoardId = board.Id, Name = "Bug", Color = "#EF4444" };
        var lTech = new Label { BoardId = board.Id, Name = "Tech Debt", Color = "#6B7280" };
        var lResearch = new Label { BoardId = board.Id, Name = "Research", Color = "#A855F7" };
        db.Labels.AddRange(lFeature, lBug, lTech, lResearch);

        var planned = new BoardList { BoardId = board.Id, Name = "Planned", Position = 0, CreatedAt = now };
        var active = new BoardList { BoardId = board.Id, Name = "Active", Position = 1, CreatedAt = now };
        var blocked = new BoardList { BoardId = board.Id, Name = "Blocked", Position = 2, CreatedAt = now };
        var completed = new BoardList { BoardId = board.Id, Name = "Completed", Position = 3, CreatedAt = now };
        db.Lists.AddRange(planned, active, blocked, completed);
        await db.SaveChangesAsync(ct);

        var cards = new[]
        {
            new Card { ListId = planned.Id, Title = "Bulk card import (CSV)", Description = "Upload a CSV and create cards in a target list. Needs a template file and error reporting.", Position = 0, CreatedAt = now },
            new Card { ListId = planned.Id, Title = "Board templates", Position = 1, CreatedAt = now },
            new Card { ListId = planned.Id, Title = "Mobile app (React Native)", Position = 2, CreatedAt = now },
            new Card { ListId = planned.Id, Title = "GitHub integration", Description = "Link cards to issues and PRs; auto-move on merge.", Position = 3, CreatedAt = now },

            new Card { ListId = active.Id, Title = "Real-time collaboration polish", Description = "Smooth out SSE reconnect and cursor presence.", Position = 0, DueDate = now.AddDays(14), CreatedAt = now },
            new Card { ListId = active.Id, Title = "Per-card time tracking", Description = "Start/stop timer per card, aggregate per list and per user.", Position = 1, DueDate = now.AddDays(21), CreatedAt = now },
            new Card { ListId = active.Id, Title = "Investigate Postgres connection pool tuning", Position = 2, CreatedAt = now },

            new Card { ListId = blocked.Id, Title = "Shared boards (multi-user)", Description = "Blocked on auth model redesign — waiting for Q3 OKRs.", Position = 0, CreatedAt = now },
            new Card { ListId = blocked.Id, Title = "SSO with Google / GitHub", Position = 1, CreatedAt = now },

            new Card { ListId = completed.Id, Title = "Dark mode support", Position = 0, CreatedAt = now.AddDays(-20) },
            new Card { ListId = completed.Id, Title = "Card archiving", Position = 1, CreatedAt = now.AddDays(-15) },
            new Card { ListId = completed.Id, Title = "Fix drag-and-drop between lists", Description = "Cards couldn't move between lists after the list-level drag feature was added. Fixed by switching from cdkDropListGroup to explicit cdkDropListConnectedTo.", Position = 2, CreatedAt = now.AddDays(-1) },
            new Card { ListId = completed.Id, Title = "Dockerize full stack", Position = 3, CreatedAt = now.AddDays(-1) },
        };
        db.Cards.AddRange(cards);
        await db.SaveChangesAsync(ct);

        db.CardLabels.AddRange(
            new CardLabel { CardId = cards[0].Id, LabelId = lFeature.Id },
            new CardLabel { CardId = cards[1].Id, LabelId = lFeature.Id },
            new CardLabel { CardId = cards[2].Id, LabelId = lFeature.Id },
            new CardLabel { CardId = cards[2].Id, LabelId = lResearch.Id },
            new CardLabel { CardId = cards[3].Id, LabelId = lFeature.Id },
            new CardLabel { CardId = cards[4].Id, LabelId = lFeature.Id },
            new CardLabel { CardId = cards[5].Id, LabelId = lFeature.Id },
            new CardLabel { CardId = cards[6].Id, LabelId = lTech.Id },
            new CardLabel { CardId = cards[6].Id, LabelId = lResearch.Id },
            new CardLabel { CardId = cards[7].Id, LabelId = lFeature.Id },
            new CardLabel { CardId = cards[8].Id, LabelId = lFeature.Id },
            new CardLabel { CardId = cards[9].Id, LabelId = lFeature.Id },
            new CardLabel { CardId = cards[10].Id, LabelId = lFeature.Id },
            new CardLabel { CardId = cards[11].Id, LabelId = lBug.Id },
            new CardLabel { CardId = cards[12].Id, LabelId = lTech.Id }
        );

        var ghChecklist = new Checklist { CardId = cards[3].Id, Title = "Phase 1 scope" };
        db.Checklists.Add(ghChecklist);
        await db.SaveChangesAsync(ct);
        db.ChecklistItems.AddRange(
            new ChecklistItem { ChecklistId = ghChecklist.Id, Text = "OAuth app registration flow", IsDone = false, Position = 0 },
            new ChecklistItem { ChecklistId = ghChecklist.Id, Text = "Link card ↔ issue", IsDone = false, Position = 1 },
            new ChecklistItem { ChecklistId = ghChecklist.Id, Text = "Webhook receiver for PR events", IsDone = false, Position = 2 },
            new ChecklistItem { ChecklistId = ghChecklist.Id, Text = "Auto-move card to \"Completed\" on merge", IsDone = false, Position = 3 }
        );

        await db.SaveChangesAsync(ct);
        return board.Id;
    }
}
