using Domain.Entities.Skills;
using Domain.Entities.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Persistence.Configurations;

public sealed class SkillConfiguration : IEntityTypeConfiguration<Skill>
{
    public void Configure(EntityTypeBuilder<Skill> builder)
    {
        builder.HasKey(skill => skill.Id);
        builder.Property(skill => skill.Id).ValueGeneratedNever();

        builder.Property(skill => skill.Name).IsRequired().HasMaxLength(Skill.MaxNameLength);
        builder.Property(skill => skill.Description).IsRequired().HasMaxLength(Skill.MaxDescriptionLength);

        builder.Property(skill => skill.FileId).IsRequired();
        builder.Property(skill => skill.ContentHash).IsRequired().HasMaxLength(Skill.ContentHashLength);
        builder.Property(skill => skill.SizeBytes).IsRequired();
        builder.Property(skill => skill.HasScripts).IsRequired();

        builder.Property(skill => skill.CreatedAt).IsRequired();
        builder.Property(skill => skill.UpdatedAt).IsRequired();

        // Владельца может не быть — это общий скилл, доступный всем.
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(skill => skill.UserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(skill => skill.UserId);

        // Имя уникально в пределах владельца: провайдер скиллов отбирает их по имени
        // из frontmatter, и два одноимённых скилла у одного агента были бы неразличимы.
        // Общие скиллы (UserId is null) в этот индекс не попадают — в PostgreSQL NULL
        // не равен NULL, — поэтому для них ниже отдельный индекс.
        builder.HasIndex(skill => new { skill.UserId, skill.Name }).IsUnique();

        builder.HasIndex(skill => skill.Name)
            .IsUnique()
            .HasFilter("user_id IS NULL");
    }
}
