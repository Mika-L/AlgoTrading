using System;
using System.Collections.Generic;
using System.Linq;
using ConsoleApp1.Models;

namespace ConsoleApp1.Repository
{
	/// <summary>
	/// Repository for managing Order entities in the database.
	/// </summary>
	public class OrderRepository
	{
		private readonly StockDbContext _context;

		public OrderRepository(StockDbContext context)
		{
			_context = context;
		}

		/// <summary>
		/// Adds a new order to the database.
		/// </summary>
		public void Add(Order order)
		{
			_context.Orders.Add(order);
			_context.SaveChanges();
		}

		/// <summary>
		/// Adds a new order asynchronously.
		/// </summary>
		public async Task AddAsync(Order order)
		{
			await _context.Orders.AddAsync(order);
			await _context.SaveChangesAsync();
		}

		/// <summary>
		/// Gets an order by its ID.
		/// </summary>
		public Order? GetById(int id)
		{
			return _context.Orders.FirstOrDefault(o => o.Id == id);
		}

		/// <summary>
		/// Gets an order by its ID asynchronously.
		/// </summary>
		public async Task<Order?> GetByIdAsync(int id)
		{
			return await _context.Orders.FindAsync(id);
		}

		/// <summary>
		/// Gets all orders.
		/// </summary>
		public List<Order> GetAll()
		{
			return _context.Orders.ToList();
		}

		/// <summary>
		/// Gets all orders asynchronously.
		/// </summary>
		public async Task<List<Order>> GetAllAsync()
		{
			return await Task.Run(() => _context.Orders.ToList());
		}

		/// <summary>
		/// Updates an existing order.
		/// </summary>
		public void Update(Order order)
		{
			_context.Orders.Update(order);
			_context.SaveChanges();
		}

		/// <summary>
		/// Updates an existing order asynchronously.
		/// </summary>
		public async Task UpdateAsync(Order order)
		{
			_context.Orders.Update(order);
			await _context.SaveChangesAsync();
		}

		/// <summary>
		/// Deletes an order by its ID.
		/// </summary>
		public void Delete(int id)
		{
			var order = _context.Orders.FirstOrDefault(o => o.Id == id);
			if (order != null)
			{
				_context.Orders.Remove(order);
				_context.SaveChanges();
			}
		}

		/// <summary>
		/// Deletes an order by its ID asynchronously.
		/// </summary>
		public async Task DeleteAsync(int id)
		{
			var order = await _context.Orders.FindAsync(id);
			if (order != null)
			{
				_context.Orders.Remove(order);
				await _context.SaveChangesAsync();
			}
		}
	}
}
