using Microsoft.EntityFrameworkCore;

namespace Vitality.Models.EntityClasses
{
    /// <summary>
    /// RBAC additions to <see cref="MainContext"/>, kept in their own partial file.
    /// <para>
    /// <c>MainContext.cs</c> is a 2,250-line reverse-engineered file whose
    /// <c>OnModelCreating</c> configures 89 entities, and it ends by calling the
    /// unimplemented <c>partial void OnModelCreatingPartial(ModelBuilder)</c> hook.
    /// Implementing that hook here adds the RBAC model without editing a single line
    /// of the scaffolded file - so there is no chance of disturbing an existing entity
    /// configuration, and a future re-scaffold of MainContext.cs will not wipe this out.
    /// </para>
    /// <para>
    /// Because the hook runs last, configuration here also wins over anything set
    /// earlier for the same property - useful if an RBAC column ever needs to override
    /// the scaffolded mapping.
    /// </para>
    /// </summary>
    public partial class MainContext
    {
        public virtual DbSet<SYS_Permission> SYS_Permissions { get; set; } = null!;

        public virtual DbSet<SYS_RolePermission> SYS_RolePermissions { get; set; } = null!;

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<LK_Role>(entity =>
            {
                // dbo.LK_Roles.RoleId IS an identity column, which EF already assumes
                // for an int primary key - so no ValueGeneratedNever here, and RolesRepo
                // must not set RoleId when creating a role.
                entity.Property(e => e.RoleDescription).HasMaxLength(500);

                entity.Property(e => e.IsSystemRole).HasDefaultValue(false);
                entity.Property(e => e.IsDefault).HasDefaultValue(false);
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                entity.Property(e => e.IsDeleted).HasDefaultValue(false);

                entity.Property(e => e.CreatedDateUtc)
                      .HasColumnType("datetime2(3)")
                      .HasDefaultValueSql("(sysutcdatetime())");

                entity.Property(e => e.UpdatedDateUtc).HasColumnType("datetime2(3)");
            });

            modelBuilder.Entity<SYS_Permission>(entity =>
            {
                entity.ToTable("SYS_Permission");

                entity.HasKey(e => e.PermissionId);

                entity.HasIndex(e => e.PermissionCode).IsUnique();

                entity.HasIndex(e => new { e.ModuleKey, e.DisplayOrder });

                entity.Property(e => e.PermissionCode).HasMaxLength(100).IsRequired();
                entity.Property(e => e.ModuleKey).HasMaxLength(50).IsRequired();
                entity.Property(e => e.ModuleName).HasMaxLength(100).IsRequired();
                entity.Property(e => e.ActionName).HasMaxLength(100).IsRequired();
                entity.Property(e => e.Description).HasMaxLength(500);

                entity.Property(e => e.DisplayOrder).HasDefaultValue(0);
                entity.Property(e => e.IsActive).HasDefaultValue(true);

                entity.Property(e => e.CreatedDateUtc)
                      .HasColumnType("datetime2(3)")
                      .HasDefaultValueSql("(sysutcdatetime())");
            });

            modelBuilder.Entity<SYS_RolePermission>(entity =>
            {
                entity.ToTable("SYS_RolePermission");

                entity.HasKey(e => e.RolePermissionId);

                // Enforced in the database too, so a double-submit cannot double-grant.
                entity.HasIndex(e => new { e.RoleId, e.PermissionId }).IsUnique();

                entity.Property(e => e.CreatedDateUtc)
                      .HasColumnType("datetime2(3)")
                      .HasDefaultValueSql("(sysutcdatetime())");

                entity.HasOne(d => d.Role)
                      .WithMany(p => p.SYS_RolePermissions)
                      .HasForeignKey(d => d.RoleId)
                      // Deleting a role is a soft delete, so no cascade here: an
                      // accidental hard delete should fail on the constraint rather
                      // than silently drop the grant history.
                      .OnDelete(DeleteBehavior.Restrict)
                      .HasConstraintName("FK_SYS_RolePermission_LK_Roles");

                entity.HasOne(d => d.Permission)
                      .WithMany(p => p.SYS_RolePermissions)
                      .HasForeignKey(d => d.PermissionId)
                      .OnDelete(DeleteBehavior.Cascade)
                      .HasConstraintName("FK_SYS_RolePermission_SYS_Permission");
            });
        }
    }
}
