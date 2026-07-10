using System.Reflection;
using SergioIzq.Domain.Kernel.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace SergioIzq.Infrastructure.Kernel.Persistence;

/// <summary>
/// DbContext base con las convenciones del kernel:
/// registra automáticamente las entidades del ensamblado de dominio (subclases de
/// <see cref="AbsEntity{TId}"/>), aplica las <c>IEntityTypeConfiguration</c> del ensamblado
/// del contexto derivado, elimina la propiedad <c>_domainEvents</c> del modelo, añade índice
/// único sobre Id, y optimiza <see cref="SaveChangesAsync(CancellationToken)"/> con detección
/// de cambios manual.
/// </summary>
public abstract class KernelDbContext : DbContext
{
    protected KernelDbContext(DbContextOptions options) : base(options)
    {
    }

    /// <summary>
    /// Ensamblado donde viven las entidades de dominio del consumidor
    /// (ej. <c>typeof(MiEntidad).Assembly</c>).
    /// </summary>
    protected abstract Assembly DomainAssembly { get; }

    /// <summary>
    /// Tipos de entidad a registrar. Por defecto: clases concretas del <see cref="DomainAssembly"/>
    /// que hereden (directa o indirectamente) de <see cref="AbsEntity{TId}"/>.
    /// Sobrescribir para filtrar o desactivar el escaneo.
    /// </summary>
    protected virtual IEnumerable<Type> GetDomainEntityTypes()
    {
        return DomainAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && InheritsFromAbsEntity(t));
    }

    private static bool InheritsFromAbsEntity(Type type)
    {
        var current = type.BaseType;
        while (current != null)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(AbsEntity<>))
            {
                return true;
            }
            current = current.BaseType;
        }
        return false;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        foreach (var type in GetDomainEntityTypes())
        {
            modelBuilder.Entity(type);
        }

        // Las IEntityTypeConfiguration viven en el ensamblado del contexto derivado
        // (GetType().Assembly, no el de este paquete).
        modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            // El campo de eventos de dominio no debe mapearse a la base de datos
            var domainEventsProperty = entityType.FindProperty("_domainEvents");
            if (domainEventsProperty != null)
            {
                entityType.RemoveProperty(domainEventsProperty);
            }

            // Índice único sobre Id si la configuración concreta no definió ya uno
            var idProperty = entityType.FindProperty("Id");
            if (idProperty != null)
            {
                var existingIndex = entityType.GetIndexes()
                    .FirstOrDefault(i => i.Properties.Any(p => p.Name == "Id"));

                if (existingIndex == null)
                {
                    modelBuilder.Entity(entityType.ClrType)
                        .HasIndex("Id")
                        .IsUnique();
                }
            }
        }

        base.OnModelCreating(modelBuilder);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Detección de cambios manual, una sola vez, en vez de la automática por operación
        ChangeTracker.AutoDetectChangesEnabled = false;

        try
        {
            ChangeTracker.DetectChanges();
            return await base.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            ChangeTracker.AutoDetectChangesEnabled = true;
        }
    }
}
