import { Component, OnInit, ChangeDetectorRef, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
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
        'Delete this category? Its weight range will be merged into a neighbouring category.',
      )
    )
      return;
    this.error = '';
    this.api.deleteCategory(id).subscribe({
      next: () => this.load(),
      error: (err) => {
        this.error = (err?.error?.errors ?? ['Delete failed.']).join(' ');
        this.cdr.detectChanges();
      },
    });
  }

  trackById(_: number, c: Category): number {
    return c.id;
  }
}
