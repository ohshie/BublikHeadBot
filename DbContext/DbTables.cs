using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BublikHeadBot;

    public class BotUser
    {
        [Key]
        public long Id { get; set; }
        public string Username { get; set; }

        public int MessagesCounter { get; set; }
        public int Points { get; set; }

        public ICollection<Habit?> Habits { get; set; }
    }

    public class Habit
    {
        [Key]
        public int Id { get; set; }
        [ForeignKey("User")]
        public long UserId { get; set; }

        public string HabitName { get; set; }
        public bool CompleteConfirmationPending { get; set; }
        public bool ApprovalPending { get; set; }
        public long MessageIdForApproval { get; set; }
        public long MessageIdForConfirmation { get; set; }
        public DateTime CreatedAt { get; set; }

        public BotUser User { get; set; }
        public ICollection<Agreement> Agreements { get; set; }
    }

    public class Boyan
    {
        [Key]
        public int Id { get; set; }
        [ForeignKey("User")]
        public long UserId { get; set; }
        
        public bool BoyanConfirmationPending { get; set; }
        public long MessageIdForConfirmation { get; set; }
        
        public BotUser User { get; set; }
        public ICollection<Agreement> Agreements { get; set; }
    }

    public class Agreement
    {
        [Key]
        public int Id { get; set; }
        [ForeignKey("Habit")]
        public int HabitId { get; set; }
        
        [ForeignKey("User")]
        public long AgreedByUserId { get; set; }

        public Habit TargetHabit { get; set; }
        public Boyan PotentialBoyan { get; set; }
        public BotUser AgreedByUser { get; set; }
    }