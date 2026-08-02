using Domain.Entities.Agents;
using Domain.Entities.Conversations;
using Domain.Entities.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Persistence.Configurations;

public sealed class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        builder.HasKey(conversation => conversation.Id);
        builder.Property(conversation => conversation.Id).ValueGeneratedNever();
        builder.Property(conversation => conversation.Title).IsRequired();
        builder.Property(conversation => conversation.CreatedAt).IsRequired();
        builder.Property(conversation => conversation.UpdatedAt).IsRequired();
        builder.Property(conversation => conversation.IsPinned).IsRequired();

        // json, а НЕ jsonb — и это принципиально.
        //
        // Содержимое сессии агента сериализуется полиморфно: у каждого AIContent есть дискриминатор
        // "$type", и System.Text.Json при чтении требует, чтобы он был ПЕРВЫМ свойством объекта.
        // jsonb не хранит исходный текст — он разбирает JSON в своё представление и пересортировывает
        // ключи, из-за чего "$type" уезжает с первого места, и десериализация падает с
        // «The metadata property ... is not the first property in the deserialized JSON object».
        // Тип json хранит текст дословно, поэтому порядок ключей сохраняется.
        builder.Property(conversation => conversation.AgentState).HasColumnType("json");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(conversation => conversation.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Агента удалили — чат остаётся, просто возвращается к встроенному агенту.
        // Поэтому SetNull, а не Cascade: переписку терять из-за удаления агента нельзя.
        builder.HasOne<Agent>()
            .WithMany()
            .HasForeignKey(conversation => conversation.AgentId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(conversation => conversation.Messages)
            .WithOne()
            .HasForeignKey(message => message.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.Navigation(conversation => conversation.Messages)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
