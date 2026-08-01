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

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(conversation => conversation.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(conversation => conversation.Messages)
            .WithOne()
            .HasForeignKey(message => message.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.Navigation(conversation => conversation.Messages)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
