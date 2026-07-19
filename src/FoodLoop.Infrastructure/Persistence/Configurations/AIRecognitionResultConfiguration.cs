using FoodLoop.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FoodLoop.Infrastructure.Persistence.Configurations;

public class AIRecognitionResultConfiguration : IEntityTypeConfiguration<AIRecognitionResult>
{
    public void Configure(EntityTypeBuilder<AIRecognitionResult> builder)
    {
        builder.Property(r => r.DetectedProduct).HasMaxLength(200);
        builder.Property(r => r.ExtractedText).HasMaxLength(4000);
    }
}
