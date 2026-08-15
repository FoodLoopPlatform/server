namespace FoodLoop.Domain.Enums;

public enum UploadDocumentType
{
    // Merchant Document Types
    CommercialRegistration = 0,
    TaxIdCertificate = 1,
    StoreFacilityPhoto = 2,

    // Charity Document Types
    AssociationCertificate = 3,  // شهادة أو قرار الإشهار
    CharityBylaws = 4,           // النظام الأساسي للجمعية
    BoardOfDirectorsList = 5     // كشوف أسماء مجلس الإدارة
}
