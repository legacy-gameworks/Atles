﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Atlas.Data;
using Atlas.Domain;
using Atlas.Domain.Categories;
using Atlas.Domain.Forums;
using Atlas.Domain.Members;
using Atlas.Domain.PermissionSets;
using Atlas.Domain.PermissionSets.Commands;
using Atlas.Domain.Sites;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Post = Atlas.Domain.Posts.Post;

namespace Atlas.Server.Services
{
    public class InstallationService : IInstallationService
    {
        private readonly AtlasDbContext _dbContext;
        private readonly IConfiguration _configuration;
        private readonly IServiceProvider _serviceProvider;

        public InstallationService(AtlasDbContext dbContext, 
            IConfiguration configuration, 
            IServiceProvider serviceProvider)
        {
            _dbContext = dbContext;
            _configuration = configuration;
            _serviceProvider = serviceProvider;
        }

        public async Task EnsureDefaultSiteInitializedAsync()
        {
            var roleManager = _serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = _serviceProvider.GetRequiredService<UserManager<IdentityUser>>();

            // Roles
            if (await roleManager.RoleExistsAsync(Consts.RoleNameAdmin) == false)
            {
                await roleManager.CreateAsync(new IdentityRole(Consts.RoleNameAdmin));
            }
            var roleAdmin = await roleManager.FindByNameAsync(Consts.RoleNameAdmin);

            if (await roleManager.RoleExistsAsync(Consts.RoleNameModerator) == false)
            {
                await roleManager.CreateAsync(new IdentityRole(Consts.RoleNameModerator));
            }
            var roleModerator = await roleManager.FindByNameAsync(Consts.RoleNameModerator);

            // Users
            var userAdmin = await userManager.FindByEmailAsync(_configuration["DefaultAdminUserEmail"]);
            if (userAdmin == null)
            {
                userAdmin = new IdentityUser
                {
                    Email = _configuration["DefaultAdminUserEmail"],
                    UserName = _configuration["DefaultAdminUserName"]
                };
                await userManager.CreateAsync(userAdmin, _configuration["DefaultAdminUserPassword"]);
                var code = await userManager.GenerateEmailConfirmationTokenAsync(userAdmin);
                await userManager.ConfirmEmailAsync(userAdmin, code);
            }
            await userManager.AddToRoleAsync(userAdmin, Consts.RoleNameAdmin);

            var userNormal = await userManager.FindByEmailAsync(_configuration["DefaultNormalUserEmail"]);
            if (userNormal == null)
            {
                userNormal = new IdentityUser
                {
                    Email = _configuration["DefaultNormalUserEmail"],
                    UserName = _configuration["DefaultNormalUserName"]
                };
                await userManager.CreateAsync(userNormal, _configuration["DefaultNormalUserPassword"]);
                var code = await userManager.GenerateEmailConfirmationTokenAsync(userNormal);
                await userManager.ConfirmEmailAsync(userNormal, code);
            }

            var userModerator = await userManager.FindByEmailAsync(_configuration["DefaultModeratorUserEmail"]);
            if (userModerator == null)
            {
                userModerator = new IdentityUser
                {
                    Email = _configuration["DefaultModeratorUserEmail"],
                    UserName = _configuration["DefaultModeratorUserName"]
                };
                await userManager.CreateAsync(userModerator, _configuration["DefaultModeratorUserPassword"]);
                var code = await userManager.GenerateEmailConfirmationTokenAsync(userModerator);
                await userManager.ConfirmEmailAsync(userModerator, code);
            }
            await userManager.AddToRoleAsync(userModerator, Consts.RoleNameModerator);

            // Site
            var site = await _dbContext.Sites.FirstOrDefaultAsync(x => x.Name == "Default");

            if (site != null)
            {
                return;
            }

            site = new Site("Default", "Atlas");
            _dbContext.Sites.Add(site);
            _dbContext.Events.Add(new Event(site.Id,
                null,
                EventType.Created,
                typeof(Site),
                site.Id,
                new
                {
                    site.Name,
                    site.Title
                }));

            // Members
            var memberAdmin = await _dbContext.Members.FirstOrDefaultAsync(x => x.UserId == userAdmin.Id);
            if (memberAdmin == null)
            {
                memberAdmin = new Member(userAdmin.Id, _configuration["DefaultAdminUserEmail"], _configuration["DefaultAdminUserDisplayName"]);
                _dbContext.Members.Add(memberAdmin);
                _dbContext.Events.Add(new Event(site.Id,
                    null,
                    EventType.Created,
                    typeof(Member),
                    memberAdmin.Id,
                    new
                    {
                        memberAdmin.UserId,
                        memberAdmin.Email,
                        memberAdmin.DisplayName
                    }));
            }

            var memberNormal = await _dbContext.Members.FirstOrDefaultAsync(x => x.UserId == userNormal.Id);
            if (memberNormal == null)
            {
                memberNormal = new Member(userNormal.Id, _configuration["DefaultNormalUserEmail"], _configuration["DefaultNormalUserDisplayName"]);
                _dbContext.Members.Add(memberNormal);
                _dbContext.Events.Add(new Event(site.Id,
                    null,
                    EventType.Created,
                    typeof(Member),
                    memberNormal.Id,
                    new
                    {
                        memberNormal.UserId,
                        memberNormal.Email,
                        memberNormal.DisplayName
                    }));
            }

            var memberModerator = await _dbContext.Members.FirstOrDefaultAsync(x => x.UserId == userModerator.Id);
            if (memberModerator == null)
            {
                memberModerator = new Member(userModerator.Id, _configuration["DefaultModeratorUserEmail"], _configuration["DefaultModeratorUserDisplayName"]);
                _dbContext.Members.Add(memberModerator);
                _dbContext.Events.Add(new Event(site.Id,
                    null,
                    EventType.Created,
                    typeof(Member),
                    memberModerator.Id,
                    new
                    {
                        memberModerator.UserId,
                        memberModerator.Email,
                        memberModerator.DisplayName
                    }));
            }

            // Permission Sets
            var permissionSetDefault = new PermissionSet(site.Id, "Default", new List<PermissionCommand>
            {
                new PermissionCommand{Type = PermissionType.ViewForum, RoleId = Consts.RoleIdAll},
                new PermissionCommand{Type = PermissionType.ViewTopics, RoleId = Consts.RoleIdAll},
                new PermissionCommand{Type = PermissionType.Read, RoleId = Consts.RoleIdAll},
                new PermissionCommand{Type = PermissionType.Start, RoleId = Consts.RoleIdRegistered},
                new PermissionCommand{Type = PermissionType.Reply, RoleId = Consts.RoleIdRegistered},
                new PermissionCommand{Type = PermissionType.Edit, RoleId = Consts.RoleIdRegistered},
                new PermissionCommand{Type = PermissionType.Delete, RoleId = Consts.RoleIdRegistered},
                new PermissionCommand{Type = PermissionType.Moderate, RoleId = roleModerator.Id}
            });
            _dbContext.PermissionSets.Add(permissionSetDefault);
            _dbContext.Events.Add(new Event(site.Id,
                null,
                EventType.Created,
                typeof(PermissionSet),
                permissionSetDefault.Id,
                new
                {
                    permissionSetDefault.Name
                }));

            var permissionSetMembersOnly = new PermissionSet(site.Id, "Members Only", new List<PermissionCommand>
            {
                new PermissionCommand{Type = PermissionType.ViewForum, RoleId = Consts.RoleIdRegistered},
                new PermissionCommand{Type = PermissionType.ViewTopics, RoleId = Consts.RoleIdRegistered},
                new PermissionCommand{Type = PermissionType.Read, RoleId = Consts.RoleIdRegistered},
                new PermissionCommand{Type = PermissionType.Start, RoleId = Consts.RoleIdRegistered},
                new PermissionCommand{Type = PermissionType.Reply, RoleId = Consts.RoleIdRegistered},
                new PermissionCommand{Type = PermissionType.Edit, RoleId = Consts.RoleIdRegistered},
                new PermissionCommand{Type = PermissionType.Delete, RoleId = Consts.RoleIdRegistered},
                new PermissionCommand{Type = PermissionType.Moderate, RoleId = roleModerator.Id}
            });
            _dbContext.PermissionSets.Add(permissionSetMembersOnly);
            _dbContext.Events.Add(new Event(site.Id,
                null,
                EventType.Created,
                typeof(PermissionSet),
                permissionSetMembersOnly.Id,
                new
                {
                    permissionSetMembersOnly.Name
                }));

            var permissionSetAdminOnly = new PermissionSet(site.Id, "Admin Only", new List<PermissionCommand>
            {
                new PermissionCommand{Type = PermissionType.ViewForum, RoleId = roleAdmin.Id},
                new PermissionCommand{Type = PermissionType.ViewTopics, RoleId = roleAdmin.Id},
                new PermissionCommand{Type = PermissionType.Read, RoleId = roleAdmin.Id},
                new PermissionCommand{Type = PermissionType.Start, RoleId = roleAdmin.Id},
                new PermissionCommand{Type = PermissionType.Reply, RoleId = roleAdmin.Id},
                new PermissionCommand{Type = PermissionType.Edit, RoleId = roleAdmin.Id},
                new PermissionCommand{Type = PermissionType.Delete, RoleId = roleAdmin.Id},
                new PermissionCommand{Type = PermissionType.Moderate, RoleId = roleAdmin.Id}
            });
            _dbContext.PermissionSets.Add(permissionSetAdminOnly);
            _dbContext.Events.Add(new Event(site.Id,
                null,
                EventType.Created,
                typeof(PermissionSet),
                permissionSetAdminOnly.Id,
                new
                {
                    permissionSetAdminOnly.Name
                }));

            // Categories
            var categoryGeneral = new Category(site.Id, "General", 1, permissionSetDefault.Id);
            _dbContext.Categories.Add(categoryGeneral);
            _dbContext.Events.Add(new Event(site.Id,
                null,
                EventType.Created,
                typeof(Category),
                categoryGeneral.Id,
                new
                {
                    categoryGeneral.Name,
                    categoryGeneral.PermissionSetId,
                    categoryGeneral.SortOrder
                }));

            // Forums
            var forumWelcome = new Forum(categoryGeneral.Id, "Welcome", "Welcome Forum", 1);
            _dbContext.Forums.Add(forumWelcome);
            _dbContext.Events.Add(new Event(site.Id,
                null,
                EventType.Created,
                typeof(Forum),
                forumWelcome.Id,
                new
                {
                    forumWelcome.Name,
                    forumWelcome.CategoryId,
                    forumWelcome.PermissionSetId,
                    forumWelcome.SortOrder
                }));

            var forumMembersOnly = new Forum(categoryGeneral.Id, "Members Only", "Members Only Forum", 2, permissionSetMembersOnly.Id);
            _dbContext.Forums.Add(forumMembersOnly);
            _dbContext.Events.Add(new Event(site.Id,
                null,
                EventType.Created,
                typeof(Forum),
                forumMembersOnly.Id,
                new
                {
                    forumMembersOnly.Name,
                    forumMembersOnly.CategoryId,
                    forumMembersOnly.PermissionSetId,
                    forumMembersOnly.SortOrder
                }));

            var forumAdminOnly = new Forum(categoryGeneral.Id, "Admin Only", "Admin Only Forum", 3, permissionSetAdminOnly.Id);
            _dbContext.Forums.Add(forumAdminOnly);
            _dbContext.Events.Add(new Event(site.Id,
                null,
                EventType.Created,
                typeof(Forum),
                forumAdminOnly.Id,
                new
                {
                    forumAdminOnly.Name,
                    forumAdminOnly.CategoryId,
                    forumAdminOnly.PermissionSetId,
                    forumAdminOnly.SortOrder
                }));

            // Topics
            var topicWelcome = new Post(forumWelcome.Id, memberAdmin.Id, "Welcome to Atlas!", "Welcome...", StatusType.Published);
            _dbContext.Posts.Add(topicWelcome);
            _dbContext.Events.Add(new Event(site.Id,
                topicWelcome.MemberId,
                EventType.Created,
                typeof(Post),
                topicWelcome.Id,
                new
                {
                    topicWelcome.ForumId,
                    topicWelcome.Title,
                    topicWelcome.Content,
                    topicWelcome.Status
                }));
            categoryGeneral.IncreaseTopicsCount();
            forumWelcome.IncreaseTopicsCount();
            memberAdmin.IncreaseTopicsCount();

            // Save all changes
            await _dbContext.SaveChangesAsync();
        }
    }
}