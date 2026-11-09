using Microsoft.EntityFrameworkCore;

namespace PersonaChat.Core.Storage;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    private const int PersonaIdLength = 40;
    private const int PersonaNameLength = 80;
    private const int RoleLength = 16;
    private const int ScenarioIdLength = 40;

    public DbSet<PersonaEntity> Personas => Set<PersonaEntity>();
    public DbSet<ConversationEntity> Conversations => Set<ConversationEntity>();
    public DbSet<MessageEntity> Messages => Set<MessageEntity>();
    public DbSet<MemoryNoteEntity> MemoryNotes => Set<MemoryNoteEntity>();
    public DbSet<SimulationSessionEntity> SimulationSessions => Set<SimulationSessionEntity>();
    public DbSet<SimulationMessageEntity> SimulationMessages => Set<SimulationMessageEntity>();
    public DbSet<ReportEntity> Reports => Set<ReportEntity>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<PersonaEntity>(persona =>
        {
            persona.Property(p => p.Id).HasMaxLength(PersonaIdLength);
            persona.Property(p => p.Name).HasMaxLength(PersonaNameLength);
        });

        model.Entity<ConversationEntity>(conversation =>
        {
            conversation.HasIndex(c => c.PersonaId).IsUnique();
            conversation.HasOne<PersonaEntity>().WithMany().HasForeignKey(c => c.PersonaId);
        });

        model.Entity<MessageEntity>(message =>
        {
            message.Property(m => m.Role).HasConversion<string>().HasMaxLength(RoleLength);
            message.HasIndex(m => m.ConversationId);
            message.HasOne<ConversationEntity>().WithMany().HasForeignKey(m => m.ConversationId);
        });

        model.Entity<MemoryNoteEntity>(note =>
        {
            note.HasIndex(n => n.PersonaId);
            note.HasOne<PersonaEntity>().WithMany().HasForeignKey(n => n.PersonaId);
        });

        model.Entity<SimulationSessionEntity>(session =>
        {
            session.Property(s => s.ScenarioId).HasMaxLength(ScenarioIdLength);
            session.HasIndex(s => s.ScenarioId);
        });

        model.Entity<SimulationMessageEntity>(message =>
        {
            message.Property(m => m.Role).HasConversion<string>().HasMaxLength(RoleLength);
            message.HasIndex(m => m.SessionId);
            message.HasOne<SimulationSessionEntity>().WithMany().HasForeignKey(m => m.SessionId);
        });

        model.Entity<ReportEntity>(report =>
        {
            report.Property(r => r.Kind).HasConversion<string>().HasMaxLength(RoleLength);
            report.Property(r => r.Status).HasConversion<string>().HasMaxLength(RoleLength);
            report.Property(r => r.ScenarioId).HasMaxLength(ScenarioIdLength);
            report.HasIndex(r => r.Status);
        });
    }
}
