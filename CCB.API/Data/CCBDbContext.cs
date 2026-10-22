using System;
using System.Collections.Generic;
using CCB.API.Models;
using Microsoft.EntityFrameworkCore;

namespace CCB.API.Data;

public partial class CCBDbContext : DbContext
{
    public CCBDbContext(DbContextOptions<CCBDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<BlockedContact> BlockedContacts { get; set; }

    public virtual DbSet<Business> Businesses { get; set; }

    public virtual DbSet<BusinessOwner> BusinessOwners { get; set; }

    public virtual DbSet<BusinessService> BusinessServices { get; set; }

    public virtual DbSet<Community> Communities { get; set; }

    public virtual DbSet<CommunityEvent> CommunityEvents { get; set; }

    public virtual DbSet<CommunityJob> CommunityJobs { get; set; }

    public virtual DbSet<CommunityListing> CommunityListings { get; set; }

    public virtual DbSet<CommunityMember> CommunityMembers { get; set; }

    public virtual DbSet<CommunityMessage> CommunityMessages { get; set; }

    public virtual DbSet<Contact> Contacts { get; set; }

    public virtual DbSet<Event> Events { get; set; }

    public virtual DbSet<Message> Messages { get; set; }

    public virtual DbSet<Notification> Notifications { get; set; }

    public virtual DbSet<PaymentPin> PaymentPins { get; set; }

    public virtual DbSet<PaymentRequest> PaymentRequests { get; set; }

    public virtual DbSet<PaymentVerificationCode> PaymentVerificationCodes { get; set; }

    public virtual DbSet<Profile> Profiles { get; set; }

    public virtual DbSet<ProfileContact> ProfileContacts { get; set; }

    public virtual DbSet<Product> Products { get; set; }

    public virtual DbSet<Report> Reports { get; set; }

    public virtual DbSet<Status> Statuses { get; set; }

    public virtual DbSet<Transaction> Transactions { get; set; }

    public virtual DbSet<Translation> Translations { get; set; }

    public virtual DbSet<UserAccount> UserAccounts { get; set; }

    public virtual DbSet<UserSetting> UserSettings { get; set; }

    public virtual DbSet<Order> Orders { get; set; }

public virtual DbSet<OrderItem> OrderItems { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BlockedContact>(entity =>
        {
            entity.HasKey(e => e.BlockedContactId).HasName("PK__BlockedC__92022C39CA20BEDC");

            entity.HasIndex(e => new { e.ProfileId, e.ContactId }, "UQ_BlockedContacts").IsUnique();

            entity.Property(e => e.BlockedContactId).HasColumnName("BlockedContactID");
            entity.Property(e => e.BlockedAt).HasDefaultValueSql("(sysdatetime())", "DF_BlockedContacts_BlockedAt");
            entity.Property(e => e.ContactId).HasColumnName("ContactID");
            entity.Property(e => e.ProfileId).HasColumnName("ProfileID");

            entity.HasOne(d => d.Contact).WithMany(p => p.BlockedContacts)
                .HasForeignKey(d => d.ContactId)
                .HasConstraintName("FK_BlockedContacts_Contact");

            entity.HasOne(d => d.Profile).WithMany(p => p.BlockedContacts)
                .HasForeignKey(d => d.ProfileId)
                .HasConstraintName("FK_BlockedContacts_Profile");
        });

        modelBuilder.Entity<Business>(entity =>
        {
            entity.HasKey(e => e.BusinessId).HasName("PK__Business__F1EAA34E72377283");

            entity.Property(e => e.BusinessId).HasColumnName("BusinessID");
            entity.Property(e => e.Address).HasMaxLength(500);
            entity.Property(e => e.BannerColor).HasMaxLength(20);
            entity.Property(e => e.Category).HasMaxLength(100);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())", "DF_Businesses_CreatedAt");
            entity.Property(e => e.Description).HasMaxLength(2000);
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.Phone).HasMaxLength(30);
            entity.Property(e => e.Rating).HasColumnType("decimal(3, 2)");
        });

        modelBuilder.Entity<BusinessOwner>(entity =>
        {
            entity.HasKey(e => e.BusinessOwnerId).HasName("PK__Business__68EFB941243D0889");

            entity.HasIndex(e => new { e.BusinessId, e.ProfileId }, "UQ_BusinessOwners").IsUnique();

            entity.Property(e => e.BusinessOwnerId).HasColumnName("BusinessOwnerID");
            entity.Property(e => e.BusinessId).HasColumnName("BusinessID");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())", "DF_BusinessOwners_CreatedAt");
            entity.Property(e => e.ProfileId).HasColumnName("ProfileID");

            entity.HasOne(d => d.Business).WithMany(p => p.BusinessOwners)
                .HasForeignKey(d => d.BusinessId)
                .HasConstraintName("FK_BusinessOwners_Business");

            entity.HasOne(d => d.Profile).WithMany(p => p.BusinessOwners)
                .HasForeignKey(d => d.ProfileId)
                .HasConstraintName("FK_BusinessOwners_Profile");
        });

        modelBuilder.Entity<BusinessService>(entity =>
        {
            entity.HasKey(e => e.BusinessServiceId).HasName("PK__Business__58A214378342D03B");

            entity.HasIndex(e => e.BusinessId, "IX_BusinessServices_BusinessID");

            entity.Property(e => e.BusinessServiceId).HasColumnName("BusinessServiceID");
            entity.Property(e => e.BusinessId).HasColumnName("BusinessID");
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.Price).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Business).WithMany(p => p.BusinessServices)
                .HasForeignKey(d => d.BusinessId)
                .HasConstraintName("FK_BusinessServices_Businesses");
        });

        modelBuilder.Entity<Community>(entity =>
        {
            entity.HasKey(e => e.CommunityId).HasName("PK__Communit__CCAA5B0926099EEF");

            entity.Property(e => e.CommunityId).HasColumnName("CommunityID");
            entity.Property(e => e.BannerColor).HasMaxLength(20);
            entity.Property(e => e.Category).HasMaxLength(100);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())", "DF_Communities_CreatedAt");
            entity.Property(e => e.Description).HasMaxLength(2000);
            entity.Property(e => e.Name).HasMaxLength(150);
        });

        modelBuilder.Entity<CommunityEvent>(entity =>
        {
            entity.HasKey(e => e.CommunityEventId).HasName("PK__Communit__9CE8B62173667D8A");

            entity.HasIndex(e => e.CommunityId, "IX_CommunityEvents_CommunityID");

            entity.Property(e => e.CommunityEventId).HasColumnName("CommunityEventID");
            entity.Property(e => e.CommunityId).HasColumnName("CommunityID");
            entity.Property(e => e.DateLabel).HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.Location).HasMaxLength(300);
            entity.Property(e => e.Title).HasMaxLength(200);

            entity.HasOne(d => d.Community).WithMany(p => p.CommunityEvents)
                .HasForeignKey(d => d.CommunityId)
                .HasConstraintName("FK_CommunityEvents_Communities");
        });

       modelBuilder.Entity<CommunityJob>(
    entity =>
    {
        entity.HasKey(e =>
            e.CommunityJobId
        )
        .HasName(
            "PK__Communit__2FB5710D6FC72F4F"
        );

        entity.HasIndex(
            e => e.CommunityId,
            "IX_CommunityJobs_CommunityID"
        );

        entity.HasIndex(
            e => e.PostedByProfileId,
            "IX_CommunityJobs_PostedByProfileID"
        );

        entity.Property(e =>
            e.CommunityJobId
        )
        .HasColumnName(
            "CommunityJobID"
        );

        entity.Property(e =>
            e.CommunityId
        )
        .HasColumnName(
            "CommunityID"
        );

        entity.Property(e =>
            e.PostedByProfileId
        )
        .HasColumnName(
            "PostedByProfileID"
        );

        entity.Property(e =>
            e.Title
        )
        .HasMaxLength(200);

        entity.Property(e =>
            e.Detail
        )
        .HasMaxLength(2000);

        entity.Property(e =>
            e.Company
        )
        .HasMaxLength(150);

        entity.Property(e =>
            e.Location
        )
        .HasMaxLength(200);

        entity.Property(e =>
            e.EmploymentType
        )
        .HasMaxLength(50);

        entity.Property(e =>
            e.ApplicationContact
        )
        .HasMaxLength(300);

        entity.Property(e =>
            e.PostedAt
        )
        .HasDefaultValueSql(
            "(sysdatetime())",
            "DF_CommunityJobs_PostedAt"
        );

        entity.Property(e =>
            e.IsActive
        )
        .HasDefaultValue(
            true
        );

        entity.HasOne(e =>
            e.Community
        )
        .WithMany(e =>
            e.CommunityJobs
        )
        .HasForeignKey(e =>
            e.CommunityId
        )
        .HasConstraintName(
            "FK_CommunityJobs_Communities"
        );

        entity.HasOne(e =>
            e.PostedByProfile
        )
        .WithMany()
        .HasForeignKey(e =>
            e.PostedByProfileId
        )
        .OnDelete(
            DeleteBehavior.NoAction
        )
        .HasConstraintName(
            "FK_CommunityJobs_PostedByProfile"
        );
    }
);

modelBuilder.Entity<CommunityListing>(
    entity =>
    {
        entity.HasKey(e =>
            e.CommunityListingId
        )
        .HasName(
            "PK__Communit__308054450C8752A9"
        );

        entity.HasIndex(
            e => e.CommunityId,
            "IX_CommunityListings_CommunityID"
        );

        entity.HasIndex(
            e => e.SellerProfileId,
            "IX_CommunityListings_SellerProfileID"
        );

        entity.Property(e =>
            e.CommunityListingId
        )
        .HasColumnName(
            "CommunityListingID"
        );

        entity.Property(e =>
            e.CommunityId
        )
        .HasColumnName(
            "CommunityID"
        );

        entity.Property(e =>
            e.SellerProfileId
        )
        .HasColumnName(
            "SellerProfileID"
        );

        entity.Property(e =>
            e.Title
        )
        .HasMaxLength(200);

        entity.Property(e =>
            e.Price
        )
        .HasColumnType(
            "decimal(18, 2)"
        );

        entity.Property(e =>
            e.Description
        )
        .HasMaxLength(1000);

        entity.Property(e =>
            e.Category
        )
        .HasMaxLength(100);

        entity.Property(e =>
            e.Condition
        )
        .HasColumnName("Condition")
        .HasMaxLength(50);

        entity.Property(e =>
            e.Location
        )
        .HasMaxLength(200);

        entity.Property(e =>
            e.Contact
        )
        .HasMaxLength(300);

        entity.Property(e =>
            e.ImageUrl
        )
        .HasMaxLength(500);

        entity.Property(e =>
            e.IsAvailable
        )
        .HasDefaultValue(true);

        entity.Property(e =>
            e.CreatedAt
        )
        .HasDefaultValueSql(
            "(sysdatetime())",
            "DF_CommunityListings_CreatedAt"
        );

        entity.HasOne(e =>
            e.Community
        )
        .WithMany(e =>
            e.CommunityListings
        )
        .HasForeignKey(e =>
            e.CommunityId
        )
        .HasConstraintName(
            "FK_CommunityListings_Communities"
        );

        entity.HasOne(e =>
            e.SellerProfile
        )
        .WithMany()
        .HasForeignKey(e =>
            e.SellerProfileId
        )
        .OnDelete(
            DeleteBehavior.NoAction
        )
        .HasConstraintName(
            "FK_CommunityListings_SellerProfile"
        );
    }
);

        modelBuilder.Entity<CommunityMember>(entity =>
        {
            entity.HasKey(e => e.CommunityMemberId).HasName("PK__Communit__F315FA3668DD5A7A");

            entity.HasIndex(e => e.CommunityId, "IX_CommunityMembers_CommunityID");

            entity.HasIndex(e => e.ProfileId, "IX_CommunityMembers_ProfileID");

            entity.HasIndex(e => new { e.CommunityId, e.ProfileId }, "UQ_CommunityMembers").IsUnique();

            entity.Property(e => e.CommunityMemberId).HasColumnName("CommunityMemberID");
            entity.Property(e => e.CommunityId).HasColumnName("CommunityID");
            entity.Property(e => e.JoinedAt).HasDefaultValueSql("(sysdatetime())", "DF_CommunityMembers_JoinedAt");
            entity.Property(e => e.ProfileId).HasColumnName("ProfileID");

            entity.HasOne(d => d.Community).WithMany(p => p.CommunityMembers)
                .HasForeignKey(d => d.CommunityId)
                .HasConstraintName("FK_CommunityMembers_Community");

            entity.HasOne(d => d.Profile).WithMany(p => p.CommunityMembers)
                .HasForeignKey(d => d.ProfileId)
                .HasConstraintName("FK_CommunityMembers_Profile");
        });

        modelBuilder.Entity<CommunityMessage>(
    entity =>
    {
        entity.HasKey(e =>
            e.CommunityMessageId
        )
        .HasName(
            "PK__Communit__FE366BD4C08A73D8"
        );

        entity.HasIndex(
            e => e.CommunityId,
            "IX_CommunityMessages_CommunityID"
        );

        entity.HasIndex(
            e => e.SenderProfileId,
            "IX_CommunityMessages_SenderProfileID"
        );

        entity.Property(e =>
            e.CommunityMessageId
        )
        .HasColumnName(
            "CommunityMessageID"
        );

        entity.Property(e =>
            e.CommunityId
        )
        .HasColumnName(
            "CommunityID"
        );

        entity.Property(e =>
            e.SenderProfileId
        )
        .HasColumnName(
            "SenderProfileID"
        );

        entity.Property(e =>
            e.SenderName
        )
        .HasMaxLength(100);

        entity.Property(e =>
            e.AvatarColor
        )
        .HasMaxLength(20);

        entity.Property(e =>
            e.SentAt
        )
        .HasDefaultValueSql(
            "(sysdatetime())",
            "DF_CommunityMessages_SentAt"
        );

        entity.HasOne(e =>
            e.Community
        )
        .WithMany(e =>
            e.CommunityMessages
        )
        .HasForeignKey(e =>
            e.CommunityId
        )
        .HasConstraintName(
            "FK_CommunityMessages_Communities"
        );

        entity.HasOne(e =>
            e.SenderProfile
        )
        .WithMany()
        .HasForeignKey(e =>
            e.SenderProfileId
        )
        .OnDelete(
            DeleteBehavior.NoAction
        )
        .HasConstraintName(
            "FK_CommunityMessages_SenderProfile"
        );
    }
);
        modelBuilder.Entity<Contact>(entity =>
        {
            entity.HasKey(e => e.ContactId).HasName("PK__Contacts__5C6625BB53207B5D");

            entity.Property(e => e.ContactId).HasColumnName("ContactID");
            entity.Property(e => e.ProfileId).HasColumnName("ProfileID");
            entity.Property(e => e.AvatarColor).HasMaxLength(20);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())", "DF_Contacts_CreatedAt");
            entity.Property(e => e.Language).HasMaxLength(50);
            entity.Property(e => e.Name).HasMaxLength(100);

            entity.HasOne(e => e.Profile).WithMany()
                .HasForeignKey(e => e.ProfileId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<Event>(entity =>
        {
            entity.HasKey(e => e.EventId).HasName("PK__Events__7944C870E832F982");

            entity.Property(e => e.EventId).HasColumnName("EventID");
            entity.Property(e => e.BannerColor).HasMaxLength(20);
            entity.Property(e => e.Category).HasMaxLength(100);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())", "DF_Events_CreatedAt");
            entity.Property(e => e.DateLabel).HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(2000);
            entity.Property(e => e.Location).HasMaxLength(500);
            entity.Property(e => e.Title).HasMaxLength(200);
        });

        modelBuilder.Entity<Message>(entity =>
        {
            entity.HasKey(e => e.MessageId).HasName("PK__Messages__C87C037C2EBEB2EA");

            entity.HasIndex(e => e.ContactId, "IX_Messages_ContactID");

            entity.HasIndex(e => new { e.SenderProfileId, e.RecipientProfileId, e.SentAt }, "IX_Messages_Conversation");

            entity.Property(e => e.MessageId).HasColumnName("MessageID");
            entity.Property(e => e.ContactId).HasColumnName("ContactID");
            entity.Property(e => e.SenderProfileId).HasColumnName("SenderProfileId");
            entity.Property(e => e.RecipientProfileId).HasColumnName("RecipientProfileId");
            entity.Property(e => e.MessageText).IsRequired();
            entity.Property(e => e.ReadAt).HasColumnName("ReadAt");
            entity.Property(e => e.SentAt).HasDefaultValueSql("(sysdatetime())", "DF_Messages_SentAt");

            entity.HasOne(d => d.Contact).WithMany(p => p.Messages)
                .HasForeignKey(d => d.ContactId)
                .HasConstraintName("FK_Messages_Contacts");

            entity.HasOne(e => e.SenderProfile).WithMany()
                .HasForeignKey(e => e.SenderProfileId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_Messages_SenderProfile");

            entity.HasOne(e => e.RecipientProfile).WithMany()
                .HasForeignKey(e => e.RecipientProfileId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_Messages_RecipientProfile");
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.NotificationId).HasName("PK__Notifica__20CF2E3296EB9E66");

            entity.HasIndex(e => e.ProfileId, "IX_Notifications_ProfileID");

            entity.Property(e => e.NotificationId).HasColumnName("NotificationID");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())", "DF_Notifications_CreatedAt");
            entity.Property(e => e.Message).HasMaxLength(1000);
            entity.Property(e => e.NotificationType).HasMaxLength(50);
            entity.Property(e => e.ProfileId).HasColumnName("ProfileID");
            entity.Property(e => e.Title).HasMaxLength(200);

            entity.HasOne(d => d.Profile).WithMany(p => p.Notifications)
                .HasForeignKey(d => d.ProfileId)
                .HasConstraintName("FK_Notifications_Profile");
        });

        modelBuilder.Entity<Profile>(entity =>
        {
            entity.HasKey(e => e.ProfileId).HasName("PK__Profile__290C88845C71137E");

            entity.ToTable("Profile");

            entity.Property(e => e.ProfileId).HasColumnName("ProfileID");
            entity.Property(e => e.AvatarColor).HasMaxLength(20);
            entity.Property(e => e.Balance).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())", "DF_Profile_CreatedAt");
            entity.Property(e => e.Language)
                .HasMaxLength(50)
                .HasDefaultValue("English", "DF_Profile_Language");
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.Phone).HasMaxLength(30);
            entity.Property(e => e.Username).HasMaxLength(50);
        });

        modelBuilder.Entity<ProfileContact>(entity =>
        {
            entity.HasKey(e => e.ProfileContactId).HasName("PK__ProfileC__8EF7FB28E89D2B23");

            entity.HasIndex(e => e.ContactId, "IX_ProfileContacts_ContactID");

            entity.HasIndex(e => e.ProfileId, "IX_ProfileContacts_ProfileID");

            entity.HasIndex(e => new { e.ProfileId, e.ContactId }, "UQ_ProfileContacts").IsUnique();

            entity.Property(e => e.ProfileContactId).HasColumnName("ProfileContactID");
            entity.Property(e => e.AddedAt).HasDefaultValueSql("(sysdatetime())", "DF_ProfileContacts_AddedAt");
            entity.Property(e => e.ContactId).HasColumnName("ContactID");
            entity.Property(e => e.ProfileId).HasColumnName("ProfileID");

            entity.HasOne(d => d.Contact).WithMany(p => p.ProfileContacts)
                .HasForeignKey(d => d.ContactId)
                .HasConstraintName("FK_ProfileContacts_Contact");

            entity.HasOne(d => d.Profile).WithMany(p => p.ProfileContacts)
                .HasForeignKey(d => d.ProfileId)
                .HasConstraintName("FK_ProfileContacts_Profile");
        });

        modelBuilder.Entity<Report>(entity =>
        {
            entity.HasKey(e => e.ReportId).HasName("PK__Reports__D5BD48E5C3282399");

            entity.HasIndex(e => e.ProfileId, "IX_Reports_ProfileID");

            entity.Property(e => e.ReportId).HasColumnName("ReportID");
            entity.Property(e => e.ContactId).HasColumnName("ContactID");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())", "DF_Reports_CreatedAt");
            entity.Property(e => e.Description).HasMaxLength(2000);
            entity.Property(e => e.MessageId).HasColumnName("MessageID");
            entity.Property(e => e.ProfileId).HasColumnName("ProfileID");
            entity.Property(e => e.Reason).HasMaxLength(200);
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .HasDefaultValue("Pending", "DF_Reports_Status");

            entity.HasOne(d => d.Contact).WithMany(p => p.Reports)
                .HasForeignKey(d => d.ContactId)
                .HasConstraintName("FK_Reports_Contact");

            entity.HasOne(d => d.Message).WithMany(p => p.Reports)
                .HasForeignKey(d => d.MessageId)
                .HasConstraintName("FK_Reports_Message");

            entity.HasOne(d => d.Profile).WithMany(p => p.Reports)
                .HasForeignKey(d => d.ProfileId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Reports_Profile");
        });

        modelBuilder.Entity<Status>(entity =>
        {
            entity.HasKey(e => e.StatusId).HasName("PK__Statuses__C8EE2043A71FD3F5");

            entity.HasIndex(e => e.ContactId, "IX_Statuses_ContactID");

            entity.Property(e => e.StatusId).HasColumnName("StatusID");
            entity.Property(e => e.BackgroundColor).HasMaxLength(20);
            entity.Property(e => e.Caption).HasMaxLength(1000);
            entity.Property(e => e.ContactId).HasColumnName("ContactID");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())", "DF_Statuses_CreatedAt");

            entity.HasOne(d => d.Contact).WithMany(p => p.Statuses)
                .HasForeignKey(d => d.ContactId)
                .HasConstraintName("FK_Statuses_Contacts");
        });

        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.HasKey(e => e.TransactionId).HasName("PK__Transact__55433A4BAE03CB5A");

            entity.HasIndex(e => e.TransactionDate, "IX_Transactions_TransactionDate");
            entity.HasIndex(e => e.SenderProfileId, "IX_Transactions_SenderProfileID");
            entity.HasIndex(e => e.RecipientProfileId, "IX_Transactions_RecipientProfileID");

            entity.Property(e => e.TransactionId).HasColumnName("TransactionID");
            entity.Property(e => e.SenderProfileId).HasColumnName("SenderProfileID");
            entity.Property(e => e.RecipientProfileId).HasColumnName("RecipientProfileID");
            entity.Property(e => e.Amount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Counterparty).HasMaxLength(150);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.Direction).HasMaxLength(20);
            entity.Property(e => e.Reference).HasMaxLength(100);
            entity.Property(e => e.Status).HasMaxLength(20).HasDefaultValue("Completed");
            entity.Property(e => e.TransactionDate).HasDefaultValueSql("(sysdatetime())", "DF_Transactions_Date");

            entity.HasOne(e => e.SenderProfile).WithMany()
                .HasForeignKey(e => e.SenderProfileId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_Transactions_SenderProfile");

            entity.HasOne(e => e.RecipientProfile).WithMany()
                .HasForeignKey(e => e.RecipientProfileId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_Transactions_RecipientProfile");
        });

        modelBuilder.Entity<PaymentPin>(entity =>
        {
            entity.ToTable("PaymentPins");

            entity.HasKey(e => e.PaymentPinId).HasName("PK_PaymentPins");
            entity.HasIndex(e => e.ProfileId, "UQ_PaymentPins_ProfileID").IsUnique();

            entity.Property(e => e.PaymentPinId).HasColumnName("PaymentPinID");
            entity.Property(e => e.ProfileId).HasColumnName("ProfileID");
            entity.Property(e => e.PinHash).HasMaxLength(255);
            entity.Property(e => e.FailedAttempts).HasDefaultValue(0);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(e => e.Profile).WithOne()
                .HasForeignKey<PaymentPin>(e => e.ProfileId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_PaymentPins_Profile");
        });

       modelBuilder.Entity<PaymentVerificationCode>(
    entity =>
    {
        entity.ToTable(
            "PaymentVerificationCodes"
        );

        entity.HasKey(e =>
            e.PaymentVerificationCodeId
        )
        .HasName(
            "PK_PaymentVerificationCodes"
        );

        entity.HasIndex(
            e => e.ChallengeId,
            "UQ_PaymentCodes_ChallengeID"
        )
        .IsUnique();

        entity.HasIndex(
            e => e.SenderProfileId,
            "IX_PaymentCodes_SenderProfileID"
        );

        entity.HasIndex(
            e => e.ExpiresAt,
            "IX_PaymentCodes_ExpiresAt"
        );

        entity.HasIndex(
            e => e.PaymentRequestId,
            "IX_PaymentVerificationCodes_PaymentRequestID"
        );

        entity.Property(e =>
            e.PaymentVerificationCodeId
        )
        .HasColumnName(
            "PaymentVerificationCodeID"
        );

        entity.Property(e => e.ChallengeId)
            .HasColumnName("ChallengeID")
            .HasDefaultValueSql("(newid())");

        entity.Property(e =>
            e.SenderProfileId
        )
        .HasColumnName(
            "SenderProfileID"
        );

        entity.Property(e =>
            e.RecipientProfileId
        )
        .HasColumnName(
            "RecipientProfileID"
        );

        entity.Property(e =>
            e.PaymentRequestId
        )
        .HasColumnName(
            "PaymentRequestID"
        );

        entity.Property(e => e.Amount)
            .HasColumnType(
                "decimal(18, 2)"
            );

        entity.Property(e => e.Reference)
            .HasMaxLength(100);

        entity.Property(e => e.CodeHash)
            .HasMaxLength(64)
            .IsFixedLength();

        entity.Property(e =>
            e.FailedAttempts
        )
        .HasDefaultValue(0);

        entity.Property(e => e.IsUsed)
            .HasDefaultValue(false);

        entity.Property(e => e.CreatedAt)
            .HasDefaultValueSql(
                "(sysutcdatetime())"
            );

        entity.HasOne(e =>
            e.SenderProfile
        )
        .WithMany()
        .HasForeignKey(e =>
            e.SenderProfileId
        )
        .OnDelete(
            DeleteBehavior.NoAction
        )
        .HasConstraintName(
            "FK_PaymentCodes_Sender"
        );

        entity.HasOne(e =>
            e.RecipientProfile
        )
        .WithMany()
        .HasForeignKey(e =>
            e.RecipientProfileId
        )
        .OnDelete(
            DeleteBehavior.NoAction
        )
        .HasConstraintName(
            "FK_PaymentCodes_Recipient"
        );

        entity.HasOne(e =>
            e.PaymentRequest
        )
        .WithMany()
        .HasForeignKey(e =>
            e.PaymentRequestId
        )
        .OnDelete(
            DeleteBehavior.NoAction
        )
        .HasConstraintName(
            "FK_PaymentVerificationCodes_PaymentRequests"
        );
    }
);

        modelBuilder.Entity<PaymentRequest>(entity =>
        {
            entity.ToTable("PaymentRequests");

            entity.HasKey(e => e.PaymentRequestId)
                .HasName("PK_PaymentRequests");

            entity.HasIndex(
                    e => new { e.RequesterProfileId, e.CreatedAt },
                    "IX_PaymentRequests_Requester")
                .IsDescending(false, true);

            entity.HasIndex(
                    e => new { e.PayerProfileId, e.CreatedAt },
                    "IX_PaymentRequests_Payer")
                .IsDescending(false, true);

            entity.HasIndex(e => e.Status, "IX_PaymentRequests_Status");

            entity.Property(e => e.PaymentRequestId)
                .HasColumnName("PaymentRequestID");
            entity.Property(e => e.RequesterProfileId)
                .HasColumnName("RequesterProfileID");
            entity.Property(e => e.PayerProfileId)
                .HasColumnName("PayerProfileID");
            entity.Property(e => e.TransactionId)
                .HasColumnName("TransactionID");
            entity.Property(e => e.Amount)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Reference)
                .HasMaxLength(100);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValue("Pending");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne(e => e.RequesterProfile).WithMany()
                .HasForeignKey(e => e.RequesterProfileId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_PaymentRequests_Requester");

            entity.HasOne(e => e.PayerProfile).WithMany()
                .HasForeignKey(e => e.PayerProfileId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_PaymentRequests_Payer");

            entity.HasOne(e => e.Transaction).WithMany()
                .HasForeignKey(e => e.TransactionId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_PaymentRequests_Transaction");
        });

        modelBuilder.Entity<Translation>(entity =>
        {
            entity.HasKey(e => e.TranslationId).HasName("PK__Translat__663DA0ACFD591B00");

            entity.HasIndex(e => e.MessageId, "IX_Translations_MessageID");

            entity.Property(e => e.TranslationId).HasColumnName("TranslationID");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())", "DF_Translations_CreatedAt");
            entity.Property(e => e.MessageId).HasColumnName("MessageID");
            entity.Property(e => e.SourceLanguage).HasMaxLength(50);
            entity.Property(e => e.TargetLanguage).HasMaxLength(50);

            entity.HasOne(d => d.Message).WithMany(p => p.Translations)
                .HasForeignKey(d => d.MessageId)
                .HasConstraintName("FK_Translations_Message");
        });

        modelBuilder.Entity<UserAccount>(entity =>
        {
            entity.ToTable("UserAccount");

            entity.HasIndex(e => e.Email, "UQ_UserAccount_Email").IsUnique();

            entity.HasIndex(e => e.ProfileId, "UQ_UserAccount_ProfileId").IsUnique();

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Email).HasMaxLength(255);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.PasswordHash).HasMaxLength(255);

            entity.HasOne(d => d.Profile).WithOne(p => p.UserAccount)
                .HasForeignKey<UserAccount>(d => d.ProfileId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UserAccount_Profile");
        });

        modelBuilder.Entity<UserSetting>(entity =>
        {
            entity.HasKey(e => e.UserSettingId).HasName("PK__UserSett__C40DB81FFB9985A2");

            entity.HasIndex(e => e.ProfileId, "UQ__UserSett__290C8885E6704349").IsUnique();

            entity.Property(e => e.UserSettingId).HasColumnName("UserSettingID");
            entity.Property(e => e.NotificationsEnabled).HasDefaultValue(true, "DF_UserSettings_Notifications");
            entity.Property(e => e.ProfileId).HasColumnName("ProfileID");
            entity.Property(e => e.ReadReceiptsEnabled).HasDefaultValue(true, "DF_UserSettings_ReadReceipts");
            entity.Property(e => e.TranslationEnabled).HasDefaultValue(true, "DF_UserSettings_Translation");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(sysdatetime())", "DF_UserSettings_UpdatedAt");

            entity.HasOne(d => d.Profile).WithOne(p => p.UserSetting)
                .HasForeignKey<UserSetting>(d => d.ProfileId)
                .HasConstraintName("FK_UserSettings_Profile");
        });
        modelBuilder.Entity<Product>(entity =>
{
    entity.HasKey(e => e.ProductId)
        .HasName("PK_Products");

    entity.HasIndex(e => e.BusinessId)
        .HasDatabaseName("IX_Products_BusinessId");

    entity.Property(e => e.Name)
        .HasMaxLength(150);

    entity.Property(e => e.Description)
        .HasMaxLength(500);

    entity.Property(e => e.Category)
        .HasMaxLength(100);

    entity.Property(e => e.Price)
        .HasColumnType("decimal(10, 2)");

    entity.Property(e => e.ImageUrl)
        .HasMaxLength(500);

    entity.Property(e => e.StockQuantity)
        .HasDefaultValue(0);

    entity.Property(e => e.IsAvailable)
        .HasDefaultValue(true);

    entity.Property(e => e.IsFeatured)
        .HasDefaultValue(false);

    entity.Property(e => e.CreatedAt)
        .HasDefaultValueSql("(sysutcdatetime())");

    entity.HasOne(d => d.Business)
        .WithMany(p => p.Products)
        .HasForeignKey(d => d.BusinessId)
        .OnDelete(DeleteBehavior.Restrict)
        .HasConstraintName("FK_Products_Businesses");
});
modelBuilder.Entity<Order>(entity =>
{
    entity.HasKey(e => e.OrderId)
        .HasName("PK_Orders");

    entity.HasIndex(e => e.ProfileId)
        .HasDatabaseName("IX_Orders_ProfileId");

    entity.HasIndex(e => e.OrderNumber)
        .IsUnique()
        .HasDatabaseName("UQ_Orders_OrderNumber");

    entity.Property(e => e.OrderNumber)
        .HasMaxLength(30);

    entity.Property(e => e.FulfilmentType)
        .HasMaxLength(20);

    entity.Property(e => e.DeliveryAddress)
        .HasMaxLength(500);

    entity.Property(e => e.PaymentMethod)
        .HasMaxLength(30);

    entity.Property(e => e.Subtotal)
        .HasColumnType("decimal(10, 2)");

    entity.Property(e => e.DeliveryFee)
        .HasColumnType("decimal(10, 2)");

    entity.Property(e => e.Total)
        .HasColumnType("decimal(10, 2)");

    entity.Property(e => e.Status)
        .HasMaxLength(30)
        .HasDefaultValue("Pending");

    entity.Property(e => e.CreatedAt)
        .HasDefaultValueSql("(sysutcdatetime())");

    entity.HasOne(d => d.Profile)
        .WithMany(p => p.Orders)
        .HasForeignKey(d => d.ProfileId)
        .OnDelete(DeleteBehavior.ClientSetNull)
        .HasConstraintName("FK_Orders_Profile");
});

modelBuilder.Entity<OrderItem>(entity =>
{
    entity.HasKey(e => e.OrderItemId)
        .HasName("PK_OrderItems");

    entity.HasIndex(e => e.OrderId)
        .HasDatabaseName("IX_OrderItems_OrderId");

    entity.HasIndex(e => e.ProductId)
        .HasDatabaseName("IX_OrderItems_ProductId");

    entity.Property(e => e.ProductName)
        .HasMaxLength(150);

    entity.Property(e => e.UnitPrice)
        .HasColumnType("decimal(10, 2)");

    entity.Property(e => e.LineTotal)
        .HasColumnType("decimal(10, 2)");

    entity.HasOne(d => d.Order)
        .WithMany(p => p.OrderItems)
        .HasForeignKey(d => d.OrderId)
        .OnDelete(DeleteBehavior.Cascade)
        .HasConstraintName("FK_OrderItems_Orders");

    entity.HasOne(d => d.Product)
        .WithMany(p => p.OrderItems)
        .HasForeignKey(d => d.ProductId)
        .OnDelete(DeleteBehavior.ClientSetNull)
        .HasConstraintName("FK_OrderItems_Products");
});
        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
