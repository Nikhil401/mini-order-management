import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { CreateOrderRequest, OrderSummary } from '../models/order.models';
import { AddInventoryStockRequest, InventoryItem } from '../models/inventory.models';

@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly http = inject(HttpClient);

  getOrders(): Observable<OrderSummary[]> {
    return this.http.get<OrderSummary[]>('/orders');
  }

  createOrder(request: CreateOrderRequest): Observable<OrderSummary> {
    return this.http.post<OrderSummary>('/orders', request);
  }

  getInventory(): Observable<InventoryItem[]> {
    return this.http.get<InventoryItem[]>('/inventory');
  }

  addInventoryStock(request: AddInventoryStockRequest): Observable<InventoryItem> {
    return this.http.post<InventoryItem>('/inventory/stock', request);
  }

  getHealth(service: 'order' | 'inventory'): Observable<string> {
    const url = service === 'order' ? '/orders/health' : '/inventory/health';
    return this.http.get(url, { responseType: 'text' });
  }
}
