using Domain.Entities.Agents;
using Domain.Entities.Skills;
using Domain.Entities.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Persistence.Configurations;

public sealed class AgentConfiguration : IEntityTypeConfiguration<Agent>
{
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

        // Скиллы — связь многие-ко-многим через таблицу agent_skills. Отдельной сущности
        // под связь нет: кроме двух внешних ключей в ней хранить нечего.
        //
        // Каскад с обеих сторон: удалили агента — уходят его связи, но не сами скиллы;
        // удалили скилл — он пропадает у всех агентов, которым был выдан.
        builder.HasMany(agent => agent.Skills)
            .WithMany()
            .UsingEntity(
                "agent_skills",
                right => right.HasOne(typeof(Skill))
                    .WithMany()
                    .HasForeignKey("skill_id")
                    .OnDelete(DeleteBehavior.Cascade),
                left => left.HasOne(typeof(Agent))
                    .WithMany()
                    .HasForeignKey("agent_id")
                    .OnDelete(DeleteBehavior.Cascade),
                join => join.HasKey("agent_id", "skill_id"));

        // Коллекция снаружи только для чтения, поэтому EF работает с полем напрямую —
        // менять набор скиллов можно исключительно через Agent.Update.
        builder.Navigation(agent => agent.Skills).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(agent => agent.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(agent => agent.UserId);
    }
}
