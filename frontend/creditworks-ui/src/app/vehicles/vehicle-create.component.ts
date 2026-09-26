import { Component, OnInit, ChangeDetectorRef, inject } from '@angular/core';
import { FormsModule, NgForm } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { ApiService } from '../core/api.service';
import { Manufacturer } from '../core/models';
import { ErrorListComponent } from '../shared/error-list.component';

@Component({
  standalone: true,
  imports: [FormsModule, RouterLink, ErrorListComponent],
  templateUrl: './vehicle-create.component.html',
  styleUrls: ['./vehicle-create.component.scss']
})
export class VehicleCreateComponent implements OnInit {
  private api = inject(ApiService);
  private router = inject(Router);
  private cdr = inject(ChangeDetectorRef);

  manufacturers: Manufacturer[] = [];
  maxYear = new Date().getFullYear() + 1;
  submitting = false;
  errors: string[] = [];

  model = {
    ownerName: '',
    manufacturerId: 0,
    yearOfManufacture: this.maxYear - 1,
    weightKg: 0
  };

  ngOnInit(): void {
    this.api.getManufacturers()
      .pipe(finalize(() => this.cdr.detectChanges()))
      .subscribe({
        next: (m) => { this.manufacturers = m; },
        error: () => { this.errors = ['Failed to load manufacturers.']; }
      });
  }

  submit(form: NgForm): void {
    this.errors = [];
    if (form.invalid) {
      this.errors.push('Please fix the highlighted fields.');
      return;
    }
    this.submitting = true;
    this.api.createVehicle(this.model).subscribe({
      next: () => this.router.navigate(['/vehicles']),
      error: (err) => {
        this.submitting = false;
        this.errors = err?.error?.errors ?? ['Failed to save vehicle.'];
        this.cdr.detectChanges();
      }
    });
  }
}
