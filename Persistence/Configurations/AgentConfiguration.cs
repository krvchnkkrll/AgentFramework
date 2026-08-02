using System.Text.Json;
using Domain.Entities.Agents;
using Domain.Entities.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Persistence.Configurations;

public sealed class AgentConfiguration : IEntityTypeConfiguration<Agent>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public void Configure(EntityTypeBuilder<Agent> builder)
    {
        builder.HasKey(agent => agent.Id);
        builder.Property(agent => agent.Id).ValueGeneratedNever();

        builder.Property(agent => agent.Name).IsRequired().HasMaxLength(Agent.MaxNameLength);
        builder.Property(agent => agent.Description).HasMaxLength(Agent.MaxDescriptionLength);
        builder.Property(agent => agent.Icon).HasMaxLength(Agent.MaxIconLength);
        builder.Property(agent => agent.Instructions).HasMaxLength(Agent.MaxInstructionsLength);

        builder.Property(agent => agent.CreatedAt).IsRequired();
        builder.Property(agent => agent.UpdatedAt).IsRequired();

        // Skills — вычисляемая обёртка над полем _skills, самой колонки за ней нет.
        builder.Ignore(agent => agent.Skills);

        // Список имён скиллов — плоский массив строк, отдельная таблица под него избыточна.
        // Мапим поле напрямую: снаружи коллекция только для чтения, менять её можно
        // исключительно через Agent.Update.
        builder.Property<List<string>>("_skills")
            .HasColumnName("skills")
            .HasColumnType("json")
            .HasConversion(
                skills => JsonSerializer.Serialize(skills, JsonOptions),
                json => JsonSerializer.Deserialize<List<string>>(json, JsonOptions) ?? new List<string>(),
                new ValueComparer<List<string>>(
                    (left, right) => left != null && right != null && left.SequenceEqual(right),
                    skills => skills.Aggregate(0, (hash, skill) => HashCode.Combine(hash, skill.GetHashCode())),
                    skills => skills.ToList()))
            .IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(agent => agent.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(agent => agent.UserId);
    }
}
