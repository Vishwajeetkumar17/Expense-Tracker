using System.ComponentModel.DataAnnotations;

namespace ExpenseTracker.DTOs
{
    public class RegisterDto
    {
        public string Name { get; set; }
        [RegularExpression(@"[0-9]{10}", ErrorMessage = "Enter a valid Phone number")]
        public string PhoneNumber { get; set; }
        public string Password { get; set; }
    }
}