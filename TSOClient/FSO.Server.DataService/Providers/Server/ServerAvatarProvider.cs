﻿using FSO.Common.DataService.Framework;
using FSO.Common.DataService.Model;
using FSO.Common.Security;
using FSO.Server.Common;
using FSO.Server.Database.DA;
using FSO.Server.Database.DA.Avatars;
using FSO.Server.Database.DA.Lots;
using FSO.Server.Database.DA.Relationships;
using FSO.Server.Database.DA.Shards;
using Ninject;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;

namespace FSO.Common.DataService.Providers.Server
{
    public class ServerAvatarProvider : LazyDataServiceProvider<uint, Avatar>
    {
        private static Logger LOG = LogManager.GetCurrentClassLogger();
        private int ShardId;
        private IDAFactory DAFactory;

        public ServerAvatarProvider([Named("ShardId")] int shardId, IDAFactory factory)
        {
            this.ShardId = shardId;
            this.DAFactory = factory;
        }

        public override void PersistMutation(object entity, MutationType type, string path, object value)
        {
            var avatar = entity as Avatar;

            using (var db = DAFactory.Get())
            {
                switch (path)
                {
                    case "Avatar_Description":
                        db.Avatars.UpdateDescription(avatar.Avatar_Id, avatar.Avatar_Description);
                        break;
                    case "Avatar_PrivacyMode":
                        db.Avatars.UpdatePrivacyMode(avatar.Avatar_Id, avatar.Avatar_PrivacyMode);
                        break;
                }
            }
        }

        private string[] LockNames = new string[]
        {
            "AvatarSkills_LockLv_Body",
            "AvatarSkills_LockLv_Charisma",
            "AvatarSkills_LockLv_Cooking",
            "AvatarSkills_LockLv_Creativity",
            "AvatarSkills_LockLv_Logic",
            "AvatarSkills_LockLv_Mechanical"
        };

        public override void DemandMutation(object entity, MutationType type, string path, object value, ISecurityContext context)
        {
            var avatar = entity as Avatar;

            switch (path)
            {
                case "Avatar_BookmarksVec":
                case "Avatar_Description":
                    context.DemandAvatar(avatar.Avatar_Id, AvatarPermissions.WRITE);
                    var desc = value as string;
                    if (desc != null && desc.Length > 500)
                        throw new Exception("Description too long!");
                    break;
                case "Avatar_Skills.AvatarSkills_LockLv_Body":
                case "Avatar_Skills.AvatarSkills_LockLv_Charisma":
                case "Avatar_Skills.AvatarSkills_LockLv_Cooking":
                case "Avatar_Skills.AvatarSkills_LockLv_Creativity":
                case "Avatar_Skills.AvatarSkills_LockLv_Logic":
                case "Avatar_Skills.AvatarSkills_LockLv_Mechanical":
                    context.DemandAvatar(avatar.Avatar_Id, AvatarPermissions.WRITE);
                    var level = (ushort)value;
                    //need silly rules so this isnt gamed.
                    //to change on city level must not be on a lot (city must own claim), need to db query the other locks

                    var skills = avatar.Avatar_Skills;
                    var limit = avatar.Avatar_SkillsLockPoints;
                    var skillname = "lock_"+path.Substring(34).ToLower();

                    using (var da = DAFactory.Get())
                    {
                        if (((da.AvatarClaims.GetByAvatarID(avatar.Avatar_Id)?.location) ?? 0) != 0) throw new Exception("Lot owns avatar! Lock using the VM commands.");
                        if (level > limit - da.Avatars.GetOtherLocks(avatar.Avatar_Id, skillname)) throw new Exception("Cannot lock this many skills!");
                    }
                    break;
                case "Avatar_PrivacyMode":
                    context.DemandAvatar(avatar.Avatar_Id, AvatarPermissions.WRITE);
                    var mode = (byte)value;
                    if (mode > 1) throw new Exception("Invalid privacy mode!");
                    break;
                default:
                    throw new SecurityException("Field: " + path + " may not be mutated by users");
            }
        }

        protected override Avatar LazyLoad(uint key, Avatar oldVal)
        {
            using (var db = DAFactory.Get())
            {
                var avatar = db.Avatars.Get(key);
                if (avatar == null) { return null; }
                if (avatar.shard_id != ShardId) { return null; }

                var myLots = db.Roommates.GetAvatarsLots(avatar.avatar_id);
                DbLot lot = null;
                if (myLots.Count > 0) {
                    lot = db.Lots.Get(myLots.FirstOrDefault().lot_id);
                }
                List<DbJobLevel> levels = db.Avatars.GetJobLevels(key);
                List<DbRelationship> rels = db.Relationships.GetBidirectional(key);
                
                var ava = HydrateOne(avatar, lot, levels, rels);
                if (oldVal != null) ava.Avatar_IsOnline = oldVal.Avatar_IsOnline;
                return ava;
            }
        }

        private static readonly uint AVATAR_RECACHE_SECONDS = 30;

        protected override bool RequiresReload(uint key, Avatar value)
        {
            return (value != null && value.Avatar_IsOnline && Epoch.Now - value.FetchTime > AVATAR_RECACHE_SECONDS);
        }

        private Avatar HydrateOne(DbAvatar dbAvatar, DbLot dbLot, List<DbJobLevel> levels, List<DbRelationship> rels)
        {
            var result = new Avatar();
            result.Avatar_Id = dbAvatar.avatar_id;
            result.Avatar_Name = dbAvatar.name;
            result.Avatar_IsOnline = false;
            result.Avatar_Description = dbAvatar.description;
            result.Avatar_Appearance = new AvatarAppearance
            {
                AvatarAppearance_BodyOutfitID = dbAvatar.body,
                AvatarAppearance_HeadOutfitID = dbAvatar.head,
                AvatarAppearance_SkinTone = dbAvatar.skin_tone
            };
            var now = Epoch.Now;
            result.FetchTime = now;
            result.Avatar_Age = (uint)((now-dbAvatar.date)/((long)60*60*24));
            result.Avatar_Skills = new AvatarSkills
            {
                AvatarSkills_Body = dbAvatar.skill_body,
                AvatarSkills_LockLv_Body = dbAvatar.lock_body,
                AvatarSkills_Charisma = dbAvatar.skill_charisma,
                AvatarSkills_LockLv_Charisma = dbAvatar.lock_charisma,
                AvatarSkills_Cooking = dbAvatar.skill_cooking,
                AvatarSkills_LockLv_Cooking = dbAvatar.lock_cooking,
                AvatarSkills_Creativity = dbAvatar.skill_creativity,
                AvatarSkills_LockLv_Creativity = dbAvatar.lock_creativity,
                AvatarSkills_Logic = dbAvatar.skill_logic,
                AvatarSkills_LockLv_Logic = dbAvatar.lock_logic,
                AvatarSkills_Mechanical = dbAvatar.skill_mechanical,
                AvatarSkills_LockLv_Mechanical = dbAvatar.lock_mechanical
            };
            result.Avatar_PrivacyMode = dbAvatar.privacy_mode;
            result.Avatar_SkillsLockPoints = (ushort)(20 + result.Avatar_Age/7);

            result.Avatar_JobLevelVec = new List<JobLevel>();
            foreach (var level in levels)
            {
                result.Avatar_JobLevelVec.Add(new JobLevel
                {
                    JobLevel_JobType = level.job_type,
                    JobLevel_JobExperience = level.job_experience,
                    JobLevel_JobGrade = level.job_level
                });
            }
            result.Avatar_CurrentJob = dbAvatar.current_job;

            var fvec = new Dictionary<Tuple<uint, bool>, Relationship>();
            foreach (var rel in rels)
            {
                bool outgoing = false;
                uint target = 0;
                if (rel.from_id == dbAvatar.avatar_id)
                {
                    outgoing = true;
                    target = rel.to_id;
                } else target = rel.from_id;

                var tuple = new Tuple<uint, bool>(target, outgoing);
                Relationship relObj = null;
                if (!fvec.TryGetValue(tuple, out relObj))
                {
                    relObj = new Relationship
                    {
                        Relationship_IsOutgoing = outgoing,
                        Relationship_TargetID = target,
                        Relationship_CommentID = rel.comment_id ?? 0
                    };
                    fvec.Add(tuple, relObj);
                }
                
                if (rel.index == 0) relObj.Relationship_STR = (sbyte)rel.value;
                else relObj.Relationship_LTR = (sbyte)rel.value;
            }
            result.Avatar_FriendshipVec = new List<Relationship>(fvec.Values);

            if (dbLot != null){
                result.Avatar_LotGridXY = dbLot.location;
            }

            result.Avatar_BookmarksVec = new List<Bookmark>()
            {
                { new Bookmark { Bookmark_TargetID = 0x02, Bookmark_Type = (byte)BookmarkType.BOOKMARK } }
            };
            return result;
        }
    }
}
