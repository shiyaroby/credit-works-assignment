import { HttpClient, HttpParams } from '@angular/common/http';
import { environment } from '../../environments/environment';
import { Injectable } from '@angular/core';
import { Category, Manufacturer, SortDir, SortField, Vehicle } from './models';

const API = environment.apiBaseUrl;
@Injectable({ providedIn: 'root' })
export class ApiService {
  constructor(private http: HttpClient) {}

  getVehicles(sortBy: SortField, dir: SortDir) {
    const params = new HttpParams().set('sortBy', sortBy).set('dir', dir);
    return this.http.get<Vehicle[]>(`${API}/vehicles`, { params });
  }

  createVehicle(v: {
    ownerName: string;
    manufacturerId: number;
    yearOfManufacture: number;
    weightKg: number;
  }) {
    return this.http.post<Vehicle>(`${API}/vehicles`, v);
  }

  deleteVehicle(id: number) {
    return this.http.delete<void>(`${API}/vehicles/${id}`);
  }

  getManufacturers() {
    return this.http.get<Manufacturer[]>(`${API}/manufacturers`);
  }

  getCategories() {
    return this.http.get<Category[]>(`${API}/categories`);
  }

  createCategory(c: Omit<Category, 'id'>) {
    return this.http.post<Category>(`${API}/categories`, c);
  }

  updateCategory(id: number, c: Omit<Category, 'id'>) {
    return this.http.put<Category>(`${API}/categories/${id}`, c);
  }

  deleteCategory(id: number) {
    return this.http.delete<void>(`${API}/categories/${id}`);
  }

  replaceAllCategories(body: { categories: Omit<Category, 'id'>[] }) {
    return this.http.put<Category[]>(`${API}/categories/bulk`, body);
  }
  getIcons() {
    return this.http.get<string[]>(`${API}/categories/icons`);
  }
}
