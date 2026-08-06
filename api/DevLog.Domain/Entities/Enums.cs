namespace DevLog.Domain.Entities;

public enum ExperienceType
{
    Employer,
    PersonalProject,
    OpenSource,
    Freelance,
    Education
}

public enum PortfolioVisibility
{
    Public,
    Unlisted,
    Paused
}

public enum RawEntryType
{
    SprintEntry,
    Experience,
    LearningLog
}

public enum RawEntryStatus
{
    Draft,
    Sanitizing,
    PendingApproval,
    Approved,
    Rejected
}

public enum EmbeddingSourceType
{
    PublishedEntry,
    Experience
}

public enum InterviewRequestStatus
{
    Pending,
    Accepted,
    Declined
}

public enum PortfolioDiscipline
{
    SoftwareEngineer,
    ProductManager,
    BusinessAnalyst,
    DevOps,
    ScrumMaster,
    Other
}
