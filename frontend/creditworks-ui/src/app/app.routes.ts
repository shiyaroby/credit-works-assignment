import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'vehicles' },
  {
    path: 'vehicles',
    loadComponent: () => import('./vehicles/vehicle-list.component')
      .then(m => m.VehicleListComponent)
  },
  {
    path: 'vehicles/new',
    loadComponent: () => import('./vehicles/vehicle-create.component')
      .then(m => m.VehicleCreateComponent)
  },
  {
    path: 'categories',
    loadComponent: () => import('./categories/category-list.component')
      .then(m => m.CategoryListComponent)
  },
  {
    path: 'categories/new',
    loadComponent: () => import('./categories/category-edit.component')
      .then(m => m.CategoryEditComponent)
  },
  {
    path: 'categories/:id',
    loadComponent: () => import('./categories/category-edit.component')
      .then(m => m.CategoryEditComponent)
  }
];
