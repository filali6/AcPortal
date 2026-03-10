import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { EventsService } from '../../../core/services/events.service';

@Component({
  selector: 'app-axe-iam',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './axe-iam.component.html',
  styleUrl: './axe-iam.component.scss'
})
export class AxeIamComponent {

   
  eventType = '';
  loading = false;
  successMessage = '';
  errorMessage = '';

   
  constructor(
    private eventsService: EventsService,
    private router: Router
  ) {}

  publishEvent(): void {
    // Validation
    if (!this.eventType) {
      this.errorMessage = 'Veuillez sélectionner un type d\'événement';
      return;
    }

    this.loading = true;
    this.successMessage = '';
    this.errorMessage = '';

    // Appel POST /api/events/publish
    this.eventsService.publish('axeIAM', this.eventType).subscribe({
      next: () => {
        this.successMessage = `Événement "${this.eventType}" publié avec succès !`;
        this.loading = false;
        this.eventType = '';
      },
      error: () => {
        this.errorMessage = 'Erreur lors de la publication de l\'événement';
        this.loading = false;
      }
    });
  }

  goBack(): void {
    this.router.navigate(['/tools']);
  }
}