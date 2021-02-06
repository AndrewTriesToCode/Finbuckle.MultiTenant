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
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using MultiTenantIdentityDbContextShould;
using Xunit;

namespace MultiTenantEntityTypeBuilderShould
{
    public class TestDbContext : MultiTenantDbContext
    {
        private readonly Action<ModelBuilder> config;

        public TestDbContext(Action<ModelBuilder> config, DbContextOptions options) : base(
            new TenantInfo {Id = "dummy"},
            options)
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
        // public int BlogId { get; set; }
    }

    public class DynamicModelCacheKeyFactory : IModelCacheKeyFactory
    {
        public object Create(DbContext context)
        {
            return new Object(); // Never cache!
        }
    }

    public class MultiTenantEntityTypeBuilderShould
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
        public void AdjustIndexOnAdjustIndex()
        {
            IMutableIndex origIndex = null;

            using (var db = GetDbContext(builder =>
                {
                    builder.Entity<Blog>().HasIndex(e => e.BlogId);

                    origIndex = builder.Entity<Blog>().Metadata.GetIndexes().First();
                    builder.Entity<Blog>().IsMultiTenant().AdjustIndex(origIndex);
                }))
            {
                var index = db.Model.FindEntityType(typeof(Blog)).GetIndexes().First();
                Assert.Contains("BlogId", index.Properties.Select(p => p.Name));
                Assert.Contains("TenantId", index.Properties.Select(p => p.Name));
            }
        }

        [Fact]
        public void PreserveIndexNameOnAdjustIndex()
        {
            IMutableIndex origIndex = null;

            using (var db = GetDbContext(builder =>
                {
#if NET
                    builder.Entity<Blog>()
                           .HasIndex(e => e.BlogId, "CustomIndexName")
                           .HasDatabaseName("CustomIndexDbName");
#else
                    builder.Entity<Blog>().HasIndex(e => e.BlogId).HasName("CustomIndexName");
#endif
                    origIndex = builder.Entity<Blog>().Metadata.GetIndexes().First();
                    builder.Entity<Blog>().IsMultiTenant().AdjustIndex(origIndex);
                }))
            {
                var index = db.Model.FindEntityType(typeof(Blog)).GetIndexes().First();
#if NET
                Assert.Equal("CustomIndexName", index.Name);
                Assert.Equal("CustomIndexDbName", index.GetDatabaseName());
#elif NETCOREAPP3_1
                Assert.Equal("CustomIndexName", index.GetName());
#elif NETCOREAPP2_1
                Assert.Equal("CustomIndexName", index.Relational().Name);
#endif
            }
        }

        [Fact]
        public void PreserveIndexUniquenessOnAdjustIndex()
        {
            using (var db = GetDbContext(builder =>
                {
                    builder.Entity<Blog>().HasIndex(e => e.BlogId).IsUnique();
                    builder.Entity<Blog>().HasIndex(e => e.Url);

                    foreach (var index in builder.Entity<Blog>().Metadata.GetIndexes().ToList())
                        builder.Entity<Blog>().IsMultiTenant().AdjustIndex(index);
                }))
            {
                var index = db.Model.FindEntityType(typeof(Blog))
                              .GetIndexes()
                              .Single(i => i.Properties.Select(p => p.Name).Contains("BlogId"));
                Assert.True(index.IsUnique);
                index = db.Model.FindEntityType(typeof(Blog))
                          .GetIndexes()
                          .Single(i => i.Properties.Select(p => p.Name).Contains("Url"));
                Assert.False(index.IsUnique);
            }
        }

        [Fact]
        public void AdjustPrimaryKeyOnAdjustKey()
        {
            using (var db = GetDbContext(builder =>
                {
                    var key = builder.Entity<Blog>().Metadata.GetKeys().First();

                    builder.Entity<Blog>().IsMultiTenant().AdjustKey(key, builder);
                }))
            {
                var key = db.Model.FindEntityType(typeof(Blog)).GetKeys().ToList();

                Assert.Single(key);
                Assert.Equal(2, key[0].Properties.Count);
                Assert.Contains("BlogId", key[0].Properties.Select(p => p.Name));
                Assert.Contains("TenantId", key[0].Properties.Select(p => p.Name));
            }
        }

        [Fact]
        public void AdjustDependentForeignKeyOnAdjustKey()
        {
            using (var db = GetDbContext(builder =>
                {
                    var key = builder.Entity<Blog>().Metadata.GetKeys().First();

                    builder.Entity<Blog>().IsMultiTenant().AdjustKey(key, builder);
                }))
            {
                var key = db.Model.FindEntityType(typeof(Post)).GetForeignKeys().ToList();

                Assert.Single(key);
                Assert.Equal(2, key[0].Properties.Count);
                Assert.Contains("BlogId", key[0].Properties.Select(p => p.Name));
                Assert.Contains("TenantId", key[0].Properties.Select(p => p.Name));
            }
        }

        [Fact]
        public void AdjustAlternateKeyOnAdjustKey()
        {
            using (var db = GetDbContext(builder =>
                {
                    var key = builder.Entity<Blog>().HasAlternateKey(b => b.Url).Metadata;

                    builder.Entity<Blog>().IsMultiTenant().AdjustKey(key, builder);
                }))
            {
                var key = db.Model.FindEntityType(typeof(Blog)).GetKeys().Where(k => !k.IsPrimaryKey()).ToList();

                Assert.Single(key);
                Assert.Equal(2, key[0].Properties.Count);
                Assert.Contains("Url", key[0].Properties.Select(p => p.Name));
                Assert.Contains("TenantId", key[0].Properties.Select(p => p.Name));
            }
        }
    }
}