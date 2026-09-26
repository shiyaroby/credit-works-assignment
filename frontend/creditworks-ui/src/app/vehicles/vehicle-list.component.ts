import { Component, OnInit, ChangeDetectorRef, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { ApiService } from '../core/api.service';
import { SortDir, SortField, Vehicle } from '../core/models';
import { ErrorListComponent } from '../shared/error-list.component';

@Component({
  standalone: true,
  imports: [FormsModule, RouterLink, ErrorListComponent],
  templateUrl: './vehicle-list.component.html',
  styleUrls: ['./vehicle-list.component.scss']
})
export class VehicleListComponent implements OnInit {
  private api = inject(ApiService);
  private cdr = inject(ChangeDetectorRef);

  vehicles: Vehicle[] = [];
  loading = false;
  error = '';
  sortBy: SortField = 'ownerName';
  sortDir: SortDir = 'asc';

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading = true;
    this.error = '';

    this.api.getVehicles(this.sortBy, this.sortDir)
      .pipe(finalize(() => {
        this.loading = false;
        this.cdr.detectChanges();
      }))
      .subscribe({
        next: (v) => { this.vehicles = v; },
        error: () => { this.error = 'Failed to load vehicles.'; }
      });
  }

  setSort(field: SortField): void {
    if (this.sortBy === field) {
      this.sortDir = this.sortDir === 'asc' ? 'desc' : 'asc';
    } else {
      this.sortBy = field;
      this.sortDir = 'asc';
    }
    this.load();
  }

  indicator(field: SortField): string {
    if (this.sortBy !== field) return '';
    return this.sortDir === 'asc' ? '↑' : '↓';
  }

  remove(id: number): void {
    if (!confirm('Delete this vehicle?')) return;
    this.api.deleteVehicle(id).subscribe({
      next: () => this.load(),
      error: () => { this.error = 'Delete failed.'; }
    });
  }

  trackById(_: number, v: Vehicle): number { return v.id; }
}
