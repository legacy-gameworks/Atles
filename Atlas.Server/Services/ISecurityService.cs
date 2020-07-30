﻿using System.Collections.Generic;
using Atlas.Domain.PermissionSets;
using Atlas.Models;

namespace Atlas.Server.Services
{
    public interface ISecurityService
    {
        bool HasPermission(PermissionModel model);
        bool HasPermission(PermissionType type, IList<PermissionModel> models);
    }
}
