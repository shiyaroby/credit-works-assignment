import { Component, OnInit, ChangeDetectorRef, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { ApiService } from '../core/api.service';
import { Category } from '../core/models';
import { ErrorListComponent } from '../shared/error-list.component';

@Component({
  standalone: true,
  imports: [RouterLink, ErrorListComponent],
  templateUrl: './category-list.component.html',
  styleUrls: ['./category-list.component.scss'],
})
export class CategoryListComponent implements OnInit {
  private api = inject(ApiService);
  private cdr = inject(ChangeDetectorRef);
  private router = inject(Router);

  categories: Category[] = [];
  loading = false;
  error = '';

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading = true;
    this.error = '';

    this.api
      .getCategories()
      .pipe(
        finalize(() => {
          this.loading = false;
          this.cdr.detectChanges();
        }),
      )
      .subscribe({
        next: (c) => {
          this.categories = c;
        },
        error: () => {
          this.error = 'Failed to load categories.';
        },
      });
  }

  remove(id: number): void {
    if (
      !confirm(
        "Delete this category? If it would break the range rules, you'll be offered the bulk editor instead.",
      )
    )
      return;
    this.error = '';
    this.api.deleteCategory(id).subscribe({
      next: () => this.load(),
      error: (err) => {
        const body = err?.error;
        if (body?.suggestedAction === 'bulk-edit') {
          const proceed = confirm(
            'Deleting this category would leave a gap or overlap.\n\n' +
              'Open the bulk editor to remove it and redistribute the range?',
          );
          if (proceed) this.router.navigate(['/categories/bulk'], { queryParams: { remove: id } });
          else this.error = (body?.errors ?? []).join(' ');
        } else {
          this.error = (body?.errors ?? ['Delete failed.']).join(' ');
        }
      },
    });
  }

  trackById(_: number, c: Category): number {
    return c.id;
  }
}
