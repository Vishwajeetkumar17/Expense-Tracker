using System;

namespace ExpenseTracker.DTOs
// DTO -> Data Transfer Object
//It’s a simple class used to transfer data between layers of an application, usually:
//From backend → frontend(API response)
//From frontend → backend(API request)
//Between service layers inside the app

{
    public class ExpenseCreateDto
    {
        public string Description { get; set; }
        public decimal Amount { get; set; }
        public DateTime Date { get; set; }
        public int CategoryId { get; set; }
    }

    public class ExpenseUpdateDto
    {
        public string Description { get; set; }
        public decimal Amount { get; set; }
        public DateTime Date { get; set; }
        public int CategoryId { get; set; }
    }
}