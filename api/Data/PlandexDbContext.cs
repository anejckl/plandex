using Microsoft.EntityFrameworkCore;
using Plandex.Api.Models;

namespace Plandex.Api.Data;

public class PlandexDbContext : DbContext
{
    public PlandexDbContext(DbContextOptions<PlandexDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Board> Boards => Set<Board>();
    public DbSet<BoardList> Lists => Set<BoardList>();
    public DbSet<Card> Cards => Set<Card>();
    public DbSet<Label> Labels => Set<Label>();
    public DbSet<CardLabel> CardLabels => Set<CardLabel>();
    public DbSet<Checklist> Checklists => Set<Checklist>();
    public DbSet<ChecklistItem> ChecklistItems => Set<ChecklistItem>();
    public DbSet<TimeEntry> TimeEntries => Set<TimeEntry>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<BoardMember> BoardMembers => Set<BoardMember>();
    public DbSet<CardAssignee> CardAssignees => Set<CardAssignee>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>(e =>
        {
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.Email).HasMaxLength(320).IsRequired();
            e.Property(u => u.Name).HasMaxLength(200).IsRequired();
            e.Property(u => u.PasswordHash).IsRequired();
        });

        b.Entity<Board>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.HasOne(x => x.Owner)
                .WithMany(u => u.Boards)
                .HasForeignKey(x => x.OwnerId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.OwnerId);
        });

        b.Entity<BoardList>(e =>
        {
            e.ToTable("lists");
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.HasOne(x => x.Board)
                .WithMany(x => x.Lists)
                .HasForeignKey(x => x.BoardId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.BoardId, x.Position });
        });

        b.Entity<Card>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(500).IsRequired();
            e.HasOne(x => x.List)
                .WithMany(x => x.Cards)
                .HasForeignKey(x => x.ListId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.ListId, x.Position });
        });

        b.Entity<Label>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.Color).HasMaxLength(20).IsRequired();
            e.HasOne(x => x.Board)
                .WithMany(x => x.Labels)
                .HasForeignKey(x => x.BoardId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<CardLabel>(e =>
        {
            e.HasKey(x => new { x.CardId, x.LabelId });
            e.HasOne(x => x.Card)
                .WithMany(x => x.CardLabels)
                .HasForeignKey(x => x.CardId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Label)
                .WithMany(x => x.CardLabels)
                .HasForeignKey(x => x.LabelId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Checklist>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.HasOne(x => x.Card)
                .WithMany(x => x.Checklists)
                .HasForeignKey(x => x.CardId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ChecklistItem>(e =>
        {
            e.Property(x => x.Text).HasMaxLength(1000).IsRequired();
            e.HasOne(x => x.Checklist)
                .WithMany(x => x.Items)
                .HasForeignKey(x => x.ChecklistId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.ChecklistId, x.Position });
        });

        b.Entity<TimeEntry>(e =>
        {
            e.HasOne(x => x.Card)
                .WithMany(x => x.TimeEntries)
                .HasForeignKey(x => x.CardId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.User)
                .WithMany(x => x.TimeEntries)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.UserId, x.EndedAt });
            e.HasIndex(x => x.CardId);
        });

        b.Entity<RefreshToken>(e =>
        {
            e.Property(x => x.TokenHash).IsRequired();
            e.HasOne(x => x.User)
                .WithMany(u => u.RefreshTokens)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.TokenHash).IsUnique();
        });

        b.Entity<BoardMember>(e =>
        {
            e.HasKey(x => new { x.BoardId, x.UserId });
            e.Property(x => x.Role).HasConversion<int>();
            e.HasOne(x => x.Board)
                .WithMany(bd => bd.Members)
                .HasForeignKey(x => x.BoardId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.User)
                .WithMany(u => u.BoardMemberships)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.UserId);
        });

        b.Entity<CardAssignee>(e =>
        {
            e.HasKey(x => new { x.CardId, x.UserId });
            e.HasOne(x => x.Card)
                .WithMany(c => c.Assignees)
                .HasForeignKey(x => x.CardId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.User)
                .WithMany(u => u.CardAssignments)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.UserId);
        });
    }
}
