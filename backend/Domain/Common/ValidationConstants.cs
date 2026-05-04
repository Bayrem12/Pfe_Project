namespace Application.Common.Constants
{
    public static class ValidationConstants
    {
        public const string ValidationErrors = "Validation errors : ";
        public const string FirstNameMustHasValue = "FirstName must have a value : ";
        public const string CompanyNameMustHasValue = "CompanyName must have a value : ";
        public const string ContactMustHasValue = "Contact must have a value: ";
        public const string LastNameMustHasValue = "LastName must have a value : ";
        public const string EmailMustHasValue = "Email must have a value: ";

        // Project validation
        public const string ProjectNameMustHasValue = "Project name is required";
        public const string ProjectNameMaxLength = "Project name must not exceed 200 characters";
        public const string DescriptionMaxLength = "Description must not exceed 1000 characters";

        // Project Member validation
        public const string UserIdMustHasValue = "User ID is required";
        public const string ProjectIdMustHasValue = "Project ID is required";
        public const string RoleMustHasValue = "Role is required";
    }
}
