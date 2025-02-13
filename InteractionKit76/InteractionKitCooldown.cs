using System;
using SQLite;

namespace InteractionKit
{
    public class InteractionKitCooldown : ModKit.ORM.ModEntity<InteractionKitCooldown>
    {
        [AutoIncrement][PrimaryKey] public int Id { get; set; }
        public int PlayerId { get; set; }
        public long LastFrisked { get; set; }
        public long LastSteal { get; set; }
        public long LastKnockedOut { get; set; }
        public long LastRestrain { get; set; }

        public InteractionKitCooldown()
        {
        }
    }

    internal class PrimaryKeyAttribute : Attribute
    {
    }
}