import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

export interface Product {
  id: string;
  name: string;
  description: string;
  price: number;
  priceFormatted: string;
  imageUrl?: string;
  externalLink?: string;
  stockQuantity: number;
  trackStock: boolean;
  status: string;
  category?: string;
  sku?: string;
  isAvailable: boolean;
  createdAt: string;
}

export interface CreateProductPayload {
  name: string;
  description: string;
  price: number;
  imageUrl?: string;
  externalLink?: string;
  stockQuantity: number;
  trackStock: boolean;
  category?: string;
  sku?: string;
}

export interface UpdateProductPayload {
  id: string;
  name: string;
  description: string;
  price: number;
  imageUrl?: string;
  externalLink?: string;
  category?: string;
}

@Injectable({ providedIn: 'root' })
export class ProductsService {
  private readonly http = inject(HttpClient);
  private readonly base = '/v1/products';

  getAll(options?: { activeOnly?: boolean; category?: string; search?: string }): Observable<Product[]> {
    let params = new HttpParams();
    if (options?.activeOnly !== undefined) params = params.set('activeOnly', String(options.activeOnly));
    if (options?.category) params = params.set('category', options.category);
    if (options?.search) params = params.set('search', options.search);
    return this.http.get<Product[]>(this.base, { params });
  }

  getById(id: string): Observable<Product> {
    return this.http.get<Product>(`${this.base}/${id}`);
  }

  create(payload: CreateProductPayload): Observable<Product> {
    return this.http.post<Product>(this.base, payload);
  }

  update(payload: UpdateProductPayload): Observable<Product> {
    return this.http.put<Product>(`${this.base}/${payload.id}`, payload);
  }

  updateStock(id: string, quantity: number): Observable<void> {
    return this.http.patch<void>(`${this.base}/${id}/stock`, { quantity });
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`);
  }
}
