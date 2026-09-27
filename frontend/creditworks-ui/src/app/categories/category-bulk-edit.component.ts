import { Component, OnInit, ChangeDetectorRef, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { ApiService } from '../core/api.service';
import { Category } from '../core/models';
import { ErrorListComponent } from '../shared/error-list.component';

@Component({
  standalone: true,
  imports: [FormsModule, RouterLink, ErrorListComponent],
  templateUrl: './category-bulk-edit.component.html',
  styleUrls: ['./category-bulk-edit.component.scss'],
})
export class CategoryBulkEditComponent implements OnInit {
  private api = inject(ApiService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private cdr = inject(ChangeDetectorRef);

  rows: Category[] = [];
  icons: string[] = [];
  loading = false;
  submitting = false;
  errors: string[] = [];

  ngOnInit(): void {
    const removeId = Number(this.route.snapshot.queryParamMap.get('remove'));

    this.loading = true;
    this.api.getCategories().subscribe({
      next: (c) => {
        this.rows = c
          .filter((x) => !removeId || x.id !== removeId)
          .slice()
          .sort((a, b) => a.minWeightKg - b.minWeightKg)
          .map((x) => ({ ...x }));

        this.api.getIcons().subscribe({
          next: (i) => {
            this.icons = i;
            this.loading = false;
            this.cdr.detectChanges();
          },
          error: () => {
            this.errors = ['Failed to load icons.'];
            this.loading = false;
            this.cdr.detectChanges();
          },
        });
      },
      error: () => {
        this.errors = ['Failed to load categories.'];
        this.loading = false;
        this.cdr.detectChanges();
      },
    });
  }

  addRow(): void {
    this.rows.push({
      id: 0,
      name: '',
      minWeightKg: 0,
      maxWeightKg: null,
      iconName: this.icons[0] ?? 'light.svg',
    });
    this.cdr.detectChanges();
  }

  removeRow(index: number): void {
    this.rows.splice(index, 1);
    this.cdr.detectChanges();
  }

  submit(): void {
    this.errors = [];
    this.submitting = true;

    const payload = {
      categories: this.rows.map((r) => ({
        name: r.name,
        minWeightKg: Number(r.minWeightKg),
        maxWeightKg:
          r.maxWeightKg === null || (r.maxWeightKg as any) === '' ? null : Number(r.maxWeightKg),
        iconName: r.iconName,
      })),
    };

    this.api
      .replaceAllCategories(payload)
      .pipe(
        finalize(() => {
          this.submitting = false;
          this.cdr.detectChanges();
        }),
      )
      .subscribe({
        next: () => this.router.navigate(['/categories']),
        error: (err) => {
          this.errors = err?.error?.errors ?? ['Save failed.'];
        },
      });
  }

  trackByIndex(i: number): number {
    return i;
  }
}
