import { Component, ElementRef, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { TranslateModule } from '@ngx-translate/core';
import { LucideAngularModule, Sparkles } from 'lucide-angular';
import { environment } from '../../../../environments/environment';

@Component({
  selector: 'app-briefing-card',
  standalone: true,
  imports: [CommonModule, TranslateModule, LucideAngularModule],
  templateUrl: './briefing-card.component.html',
  styleUrls: ['./briefing-card.component.scss']
})
export class BriefingCardComponent {
  readonly Sparkles = Sparkles;

  isOpen = false;
  loading = false;
  briefing: string | null = null;
  errored = false;

  // On ne charge qu'une seule fois par session : si l'utilisateur rouvre
  // le panneau, on ne refait pas l'appel API à chaque fois.
  private alreadyFetched = false;

  constructor(private http: HttpClient, private elementRef: ElementRef) {}

  toggle(): void {
    this.isOpen = !this.isOpen;
    if (this.isOpen && !this.alreadyFetched) {
      this.fetchBriefing();
    }
  }

  private fetchBriefing(): void {
    this.loading = true;
    this.errored = false;
    this.http.get<{ briefing: string }>(`${environment.apiUrl}/dashboard/briefing`)
      .subscribe({
        next: (res) => {
          this.briefing = res.briefing;
          this.loading = false;
          this.alreadyFetched = true;
        },
        error: () => {
          this.loading = false;
          this.errored = true;
        }
      });
  }

  // Ferme le panneau si on clique en dehors
  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    if (this.isOpen && !this.elementRef.nativeElement.contains(event.target)) {
      this.isOpen = false;
    }
  }
}