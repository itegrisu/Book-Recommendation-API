using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using System.Text.Json;

namespace Book_Recommendation_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BooksController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IDatabase _redisDatabase;

        public BooksController(ApplicationDbContext context, IDatabase redisDatabase)
        {
            _context = context;
            _redisDatabase = redisDatabase;
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetBookById(int id)
        {
            var cacheKey = $"book:{id}";
            var cached = await _redisDatabase.StringGetAsync(cacheKey);

            // Delay the response by 3 seconds  
            await Task.Delay(3000);

            Book book;
            if (cached.HasValue)
            {
                book = JsonSerializer.Deserialize<Book>(cached!)!;
            }
            else
            {
                book = await _context.Books.FindAsync(id);
                if (book == null)
                {
                    return NotFound();
                }
                var bookJson = JsonSerializer.Serialize(book);
                await _redisDatabase.StringSetAsync(cacheKey, bookJson, TimeSpan.FromMinutes(5));
            }

            // 1) Popülerlik sayacını arttır  
            await _redisDatabase.SortedSetIncrementAsync(
                "popularBooks",           // Sorted Set’in key’i  
                id.ToString(),            // member olarak kitap ID’si  
                1                         // 1’er 1’er arttırıyoruz  
            );

            return Ok(book);
        }

        [HttpGet("Popular")]
        public async Task<IActionResult> GetPopular([FromQuery] int count = 10)
        {
            var entries = await _redisDatabase.SortedSetRangeByRankWithScoresAsync(
                "popularBooks",
                0, count - 1,
                Order.Descending
            );

            if (entries.Length == 0)
                return NoContent();

            // ID’leri alıp DB’den çek
            var ids = entries.Select(e => int.Parse(e.Element)).ToArray();
            var books = await _context.Books
                .Where(b => ids.Contains(b.Id))
                .ToListAsync();

            // Orijinal sıralamayı koru
            var result = ids
                .Select(id => books.First(b => b.Id == id))
                .ToList();

            return Ok(result);
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var cacheKey = "books";
            var cached = await _redisDatabase.StringGetAsync(cacheKey);
            if (cached.HasValue)
            {
                var books = JsonSerializer.Deserialize<List<Book>>(cached!);
                return Ok(books);
            }
            var booksFromDb = await _context.Books.ToListAsync();
            if (booksFromDb == null || !booksFromDb.Any())
            {
                return NotFound();
            }
            var booksJson = JsonSerializer.Serialize(booksFromDb);
            await _redisDatabase.StringSetAsync(cacheKey, booksJson, TimeSpan.FromMinutes(5));
            return Ok(booksFromDb);
        }

        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] string? title, [FromQuery] string? author)
        {
            var cacheKey = $"search:{title?.ToLower() ?? ""}:{author?.ToLower() ?? ""}";

            var cached = await _redisDatabase.StringGetAsync(cacheKey);
            if (cached.HasValue)
            {
                var list = JsonSerializer.Deserialize<List<Book>>(cached!);
                return Ok(list);
            }

            IQueryable<Book> query = _context.Books;
            if (!string.IsNullOrWhiteSpace(title))
                query = query.Where(b => b.Title.Contains(title!));
            if (!string.IsNullOrWhiteSpace(author))
                query = query.Where(b => b.Author.Contains(author!));

            var result = await query.ToListAsync();
            if (!result.Any())
                return NotFound();

            var payload = JsonSerializer.Serialize(result);
            await _redisDatabase.StringSetAsync(cacheKey, payload, TimeSpan.FromMinutes(5));

            return Ok(result);
        }

        [HttpPost]
        public async Task<IActionResult> CreateBook([FromBody] Book book)
        {
            if (book == null)
            {
                return BadRequest();
            }
            _context.Books.Add(book);
            await _context.SaveChangesAsync();
            var cacheKey = "books";
            await _redisDatabase.KeyDeleteAsync("books");
            return CreatedAtAction(nameof(GetBookById), new { id = book.Id }, book);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateBook(int id, [FromBody] Book book)
        {
            if (id != book.Id)
            {
                return BadRequest();
            }
            _context.Entry(book).State = EntityState.Modified;
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!BookExists(id))
                {
                    return NotFound();
                }
                throw;
            }
            var cacheKey = $"books:{id}";
            await _redisDatabase.KeyDeleteAsync($"book:{id}");
            await _redisDatabase.KeyDeleteAsync("books");
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteBook(int id)
        {
            var book = await _context.Books.FindAsync(id);
            if (book == null)
            {
                return NotFound();
            }
            _context.Books.Remove(book);
            await _context.SaveChangesAsync();
            var cacheKey = $"books:{id}";
            await _redisDatabase.KeyDeleteAsync($"book:{id}");
            await _redisDatabase.KeyDeleteAsync("books");
            return NoContent();
        }

        [HttpPost("add-Multi")]
        public async Task<IActionResult> AddMulti(List<Book> books)
        {
            if (books == null || !books.Any())
            {
                return BadRequest("The book list cannot be null or empty.");
            }

            await _context.Books.AddRangeAsync(books);
            await _context.SaveChangesAsync();

            await _redisDatabase.KeyDeleteAsync("books");

            return Ok(books);
        }

        private bool BookExists(int id)
        {
            return _context.Books.Any(e => e.Id == id);
        }
    }
}