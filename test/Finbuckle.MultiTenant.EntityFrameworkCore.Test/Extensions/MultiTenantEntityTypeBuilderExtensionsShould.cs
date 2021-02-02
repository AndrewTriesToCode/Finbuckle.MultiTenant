//    Copyright 2020 Finbuckle LLC, Andrew White, and Contributors
// 
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
// 
//        http://www.apache.org/licenses/LICENSE-2.0
// 
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

using System;
using System.Collections.Generic;
using System.Linq;
using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using MultiTenantIdentityDbContextShould;
using Xunit;

namespace MultiTenantEntityTypeBuilderExtensionsShould
{
    public class TestDbContext : MultiTenantDbContext
    {
        private readonly Action<ModelBuilder> config;

        public TestDbContext(Action<ModelBuilder> config, DbContextOptions options) : base(new TenantInfo{Id="dummy"}, options)
        {
            this.config = config;
        }

        public DbSet<Blog> Blogs { get; set; }
        public DbSet<Post> Posts { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            config(builder);
        }
    }

    public class Blog
    {
        public int BlogId { get; set; }
        public string Url { get; set; }

        public List<Post> Posts { get; set; }
    }

    public class Post
    {
        public int PostId { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }

        public Blog Blog { get; set; }
    }

    public class DynamicModelCacheKeyFactory : IModelCacheKeyFactory
    {
        public object Create(DbContext context)
        {
            return new Object(); // Never cache!
        }
    }

    public class MultiTenantEntityTypeBuilderExtensionsShould
    {
        private TestDbContext GetDbContext(Action<ModelBuilder> config)
        {
            var connection = new SqliteConnection("DataSource=:memory:");
            var options = new DbContextOptionsBuilder().UseSqlite(connection)
                                                       .ReplaceService<IModelCacheKeyFactory,
                                                           DynamicModelCacheKeyFactory>() // needed for testing only
                                                       .Options;

            var db = new TestDbContext(config, options);

            return db;
        }

        [Fact]
        public void AdjustUniqueIndexesOnAdjustUniqueIndexes()
        {
            using (var db = GetDbContext(builder =>
                {
#if NET
                    builder.Entity<Blog>()
                           .HasIndex(e => e.BlogId, nameof(Blog.BlogId))
                           .HasDatabaseName(nameof(Blog.BlogId) + "DbName")
                           .IsUnique();
                    builder.Entity<Blog>()
                           .HasIndex(e => e.Url, nameof(Blog.Url))
                           .HasDatabaseName(nameof(Blog.Url) + "DbName")
                           .IsUnique();
#else
                    builder.Entity<Blog>()
                           .HasIndex(e => e.BlogId)
                           .HasName(nameof(Blog.BlogId))
                           .IsUnique();
                    builder.Entity<Blog>()
                           .HasIndex(e => e.Url)
                           .HasName(nameof(Blog.Url))
                           .IsUnique();
#endif
                    builder.Entity<Blog>()
                           .IsMultiTenant()
                           .AdjustUniqueIndexes();
                }))
            {
                var indexes = db.Model.FindEntityType(typeof(Blog))
                                .GetIndexes()
                                .Where(i => i.IsUnique);

                foreach (var index in indexes.Where(i => i.IsUnique))
                {
                    Assert.Contains("TenantId", index.Properties.Select(p => p.Name));
                }
            }
        }

        [Fact]
        public void NotAdjustNonUniqueIndexesOnAdjustUniqueIndexes()
        {
            using (var db = GetDbContext(builder =>
                {
#if NET
                    builder.Entity<Blog>()
                           .HasIndex(e => e.BlogId, nameof(Blog.BlogId))
                           .HasDatabaseName(nameof(Blog.BlogId) + "DbName")
                           .IsUnique();
                    builder.Entity<Blog>()
                           .HasIndex(e => e.Url, nameof(Blog.Url))
                           .HasDatabaseName(nameof(Blog.Url) + "DbName");
#else
                    builder.Entity<Blog>()
                           .HasIndex(e => e.BlogId)
                           .HasName(nameof(Blog.BlogId))
                           .IsUnique();
                    builder.Entity<Blog>()
                           .HasIndex(e => e.Url)
                           .HasName(nameof(Blog.Url));
#endif
                    builder.Entity<Blog>()
                           .IsMultiTenant()
                           .AdjustUniqueIndexes();
                }))
            {
                var indexes = db.Model.FindEntityType(typeof(Blog))
                                .GetIndexes()
                                .Where(i => i.IsUnique);

                foreach (var index in indexes.Where(i => !i.IsUnique))
                {
                    Assert.DoesNotContain("TenantId", index.Properties.Select(p => p.Name));
                }
            }
        }

        [Fact]
        public void AdjustAllIndexesOnAdjustAllIndexes()
        {
            using (var db = GetDbContext(builder =>
                {
#if NET
                    builder.Entity<Blog>()
                           .HasIndex(e => e.BlogId, nameof(Blog.BlogId))
                           .HasDatabaseName(nameof(Blog.BlogId) + "DbName")
                           .IsUnique();
                    builder.Entity<Blog>()
                           .HasIndex(e => e.Url, nameof(Blog.Url))
                           .HasDatabaseName(nameof(Blog.Url) + "DbName");
#else
                    builder.Entity<Blog>()
                           .HasIndex(e => e.BlogId)
                           .HasName(nameof(Blog.BlogId))
                           .IsUnique();
                    builder.Entity<Blog>()
                           .HasIndex(e => e.Url)
                           .HasName(nameof(Blog.Url));
#endif
                    builder.Entity<Blog>()
                           .IsMultiTenant()
                           .AdjustAllIndexes();
                }))
            {
                var indexes = db.Model.FindEntityType(typeof(Blog))
                                .GetIndexes()
                                .Where(i => i.IsUnique);

                foreach (var index in indexes)
                {
                    Assert.Contains("TenantId", index.Properties.Select(p => p.Name));
                }
            }
        }
    }
}