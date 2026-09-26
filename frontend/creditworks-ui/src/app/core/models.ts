export interface Manufacturer {
  id: number;
  name: string;
}

export interface Vehicle {
  id: number;
  ownerName: string;
  manufacturerId: number;
  manufacturerName: string;
  yearOfManufacture: number;
  weightKg: number;
  categoryId: number | null;
  categoryName: string | null;
  categoryIcon: string | null;
}

export interface Category {
  id: number;
  name: string;
  minWeightKg: number;
  maxWeightKg: number | null;
  iconName: string;
}

export type SortField = 'ownerName' | 'manufacturer' | 'year' | 'weight';
export type SortDir = 'asc' | 'desc';
