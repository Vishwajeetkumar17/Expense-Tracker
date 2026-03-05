using ExpenseTracker.Data;
using ExpenseTracker.DTOs;
using ExpenseTracker.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text;

namespace ExpenseTracker.Controllers
{
    [Authorize]
    public class ExpensesController : Controller
    {
        private readonly AppDbContext _context;
        public ExpensesController(AppDbContext context)
        {
            _context = context;
        }

        // ================= DASHBOARD =================

        public async Task<IActionResult> Index(string search, int? categoryId, DateTime? startDate, DateTime? endDate, int page = 1)
        {
            int pageSize = 5;

            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == userId);

            var query = _context.Expenses
                .Include(e => e.Category)
                .Where(e => e.UserId == userId)
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
                query = query.Where(e => e.Description.Contains(search));

            if (categoryId.HasValue)
                query = query.Where(e => e.CategoryId == categoryId);

            if (startDate.HasValue)
                query = query.Where(e => e.Date >= startDate);

            if (endDate.HasValue)
                query = query.Where(e => e.Date <= endDate);

            int totalExpenses = await query.CountAsync();

            var expenses = await query
                .OrderByDescending(e => e.Date)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.TotalTransactions = expenses.Count();

            ViewBag.AverageExpense = expenses.Any()
                ? Math.Round(expenses.Average(e => e.Amount), 2)
                : 0;

            var topCategory = expenses
                .GroupBy(e => e.Category.Name)
                .Select(g => new
                {
                    Category = g.Key,
                    Total = g.Sum(x => x.Amount)
                })
                .OrderByDescending(x => x.Total)
                .FirstOrDefault();

            ViewBag.TopCategory = topCategory?.Category ?? "No Data";
            ViewBag.TopCategoryAmount = topCategory?.Total ?? 0;

            ViewBag.TotalPages = (int)Math.Ceiling((double)totalExpenses / pageSize);
            ViewBag.CurrentPage = page;

            ViewBag.Categories = await _context.Categories.ToListAsync();
            ViewBag.UserName = user?.Name;
            ViewBag.PhoneNumber = user?.PhoneNumber;

            var currentMonth = DateTime.Now.Month;
            var currentYear = DateTime.Now.Year;

            var monthlyExpenses = expenses
                .Where(e => e.Date.Month == currentMonth && e.Date.Year == currentYear)
                .Sum(e => e.Amount);

            var budget = await _context.Budgets
                .FirstOrDefaultAsync(b =>
                    b.UserId == userId &&
                    b.Month.Month == currentMonth &&
                    b.Month.Year == currentYear);

            ViewBag.MonthlySpent = monthlyExpenses;
            ViewBag.Budget = budget?.MonthlyBudget ?? 0;

            if (ViewBag.Budget > 0)
            {
                ViewBag.Remaining = ViewBag.Budget - monthlyExpenses;

                double percent = ((double)monthlyExpenses / (double)ViewBag.Budget) * 100;

                ViewBag.Percentage = percent;
                ViewBag.BudgetPercent = percent;
            }
            else
            {
                ViewBag.Remaining = 0;
                ViewBag.Percentage = 0;
                ViewBag.BudgetPercent = 0;
            }

            ViewBag.RecentExpenses = expenses
                .OrderByDescending(e => e.Date)
                .Take(5)
                .ToList();

            return View(expenses);
        }

        // ================= ANALYTICS PAGE =================

        public async Task<IActionResult> Analytics()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

            var expenses = await _context.Expenses
                .Include(e => e.Category)
                .Where(e => e.UserId == userId)
                .ToListAsync();

            // Total Expense
            ViewBag.TotalExpense = expenses.Sum(e => e.Amount);

            // Monthly Chart
            var monthlyData = expenses
                .GroupBy(e => e.Date.Month)
                .Select(g => new
                {
                    Month = g.Key,
                    Total = g.Sum(e => e.Amount)
                })
                .OrderBy(x => x.Month)
                .ToList();

            ViewBag.MonthLabels = monthlyData.Select(x => x.Month).ToArray();
            ViewBag.MonthTotals = monthlyData.Select(x => x.Total).ToArray();

            // Category Chart
            var categoryData = expenses
                .GroupBy(e => e.Category.Name)
                .Select(g => new
                {
                    Category = g.Key,
                    Total = g.Sum(e => e.Amount)
                })
                .ToList();

            ViewBag.CategoryLabels = categoryData.Select(x => x.Category).ToArray();
            ViewBag.CategoryTotals = categoryData.Select(x => x.Total).ToArray();

            return View();
        }


        // ================= CREATE EXPENSE =================

        public IActionResult Create()
        {
            ViewBag.Categories = _context.Categories.ToList();
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Create(ExpenseCreateDto dto)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Categories = _context.Categories.ToList();
                return View(dto);
            }

            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

            var expense = new Expense
            {
                Description = dto.Description,
                Amount = dto.Amount,
                Date = dto.Date,
                CategoryId = dto.CategoryId,
                UserId = userId
            };

            _context.Expenses.Add(expense);
            await _context.SaveChangesAsync();

            return RedirectToAction("Index");
        }


        // ================= EDIT EXPENSE =================

        public async Task<IActionResult> Edit(int id)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

            var expense = await _context.Expenses
                .FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId);

            if (expense == null)
                return NotFound();

            var dto = new ExpenseUpdateDto
            {
                Description = expense.Description,
                Amount = expense.Amount,
                Date = expense.Date,
                CategoryId = expense.CategoryId
            };

            ViewBag.Categories = _context.Categories.ToList();
            ViewBag.ExpenseId = expense.Id;

            return View(dto);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(int id, ExpenseUpdateDto dto)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Categories = _context.Categories.ToList();
                return View(dto);
            }

            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

            var expense = await _context.Expenses
                .FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId);

            if (expense == null)
                return NotFound();

            expense.Description = dto.Description;
            expense.Amount = dto.Amount;
            expense.Date = dto.Date;
            expense.CategoryId = dto.CategoryId;

            await _context.SaveChangesAsync();

            return RedirectToAction("Index");
        }


        // ================= EXPENSE DETAILS =================

        public async Task<IActionResult> Details(int id)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

            var expense = await _context.Expenses
                .Include(e => e.Category)
                .FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId);

            if (expense == null)
                return NotFound();

            return View(expense);
        }


        // ================= DELETE EXPENSE =================

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

            var expense = await _context.Expenses
                .FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId);

            if (expense == null)
                return NotFound();

            _context.Expenses.Remove(expense);
            await _context.SaveChangesAsync();

            return RedirectToAction("Index");
        }

        // ================= EXPORT EXPENSE =================
        public async Task<IActionResult> ExportCsv()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

            var expenses = await _context.Expenses
                .Include(e => e.Category)
                .Where(e => e.UserId == userId)
                .OrderByDescending(e => e.Date)
                .ToListAsync();

            var builder = new StringBuilder();

            builder.AppendLine("Description,Category,Amount,Date");

            foreach (var expense in expenses)
            {
                builder.AppendLine(
                    $"{expense.Description}," +
                    $"{expense.Category?.Name}," +
                    $"{expense.Amount:F2}," +
                    $"'{expense.Date:dd-MM-yyyy}'");
            }

            return File(
                Encoding.UTF8.GetBytes(builder.ToString()),
                "text/csv",
                "expenses.csv");
        }

        // ================= SET BUDGET =================

        [HttpGet]
        public async Task<IActionResult> SetBudget()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

            var currentMonth = DateTime.Now.Month;
            var currentYear = DateTime.Now.Year;

            var budget = await _context.Budgets
                .FirstOrDefaultAsync(b =>
                    b.UserId == userId &&
                    b.Month.Month == currentMonth &&
                    b.Month.Year == currentYear);

            return View(budget);
        }

        [HttpPost]
        public async Task<IActionResult> SetBudget(decimal monthlyBudget)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

            var monthDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);

            var existingBudget = await _context.Budgets
                .FirstOrDefaultAsync(b =>
                    b.UserId == userId &&
                    b.Month.Month == monthDate.Month &&
                    b.Month.Year == monthDate.Year);

            if (existingBudget == null)
            {
                var budget = new Budget
                {
                    UserId = userId,
                    MonthlyBudget = monthlyBudget,
                    Month = monthDate
                };

                _context.Budgets.Add(budget);
            }
            else
            {
                existingBudget.MonthlyBudget = monthlyBudget;
            }

            await _context.SaveChangesAsync();

            return RedirectToAction("Index");
        }
    }
}
